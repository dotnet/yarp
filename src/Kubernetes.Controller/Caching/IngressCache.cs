// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Yarp.Kubernetes.Controller.Certificates;
using Yarp.Kubernetes.Controller.Services;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// ICache service interface holds onto the least amount of data necessary
/// for <see cref="IReconciler"/> to process work.
/// </summary>
public class IngressCache : ICache
{
    private readonly object _sync = new object();
    private readonly Dictionary<string, IngressClassData> _ingressClassData = new Dictionary<string, IngressClassData>();
    private readonly Dictionary<string, NamespaceCache> _namespaceCaches = new Dictionary<string, NamespaceCache>();
    private readonly YarpOptions _options;
    private readonly IServerCertificateSelector _certificateSelector;
    private readonly ICertificateHelper _certificateHelper;
    private readonly ILogger<IngressCache> _logger;

    private bool _isDefaultController;

    public IngressCache(IOptions<YarpOptions> options, IServerCertificateSelector certificateSelector, ICertificateHelper certificateHelper, ILogger<IngressCache> logger)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        ArgumentNullException.ThrowIfNull(certificateSelector);
        ArgumentNullException.ThrowIfNull(certificateHelper);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _certificateSelector = certificateSelector;
        _certificateHelper = certificateHelper;
        _logger = logger;
    }

    public void Update(WatchEventType eventType, V1IngressClass ingressClass)
    {
        ArgumentNullException.ThrowIfNull(ingressClass);

        if (!string.Equals(_options.ControllerClass, ingressClass.Spec.Controller, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Ignoring {IngressClassNamespace}/{IngressClassName} as the spec.controller is not the same as this ingress",
                ingressClass.Metadata.NamespaceProperty,
                ingressClass.Metadata.Name);
            return;
        }

        var ingressClassName = ingressClass.Name();
        lock (_sync)
        {
            if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
            {
                _ingressClassData[ingressClassName] = new IngressClassData(ingressClass);
            }
            else if (eventType == WatchEventType.Deleted)
            {
                _ingressClassData.Remove(ingressClassName);
            }

            _isDefaultController = _ingressClassData.Values.Any(ic => ic.IsDefault);
        }
    }

    public bool Update(WatchEventType eventType, V1Ingress ingress)
    {
        ArgumentNullException.ThrowIfNull(ingress);

        Namespace(ingress.Namespace()).Update(eventType, ingress);
        return true;
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Service service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return Namespace(service.Namespace()).Update(eventType, service);
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints)
    {
        return Namespace(endpoints.Namespace()).Update(eventType, endpoints);
    }

    public void Update(WatchEventType eventType, V1Secret secret)
    {
        var namespacedName = NamespacedName.From(secret);
        _logger.LogDebug("Found secret '{NamespacedName}'. Checking against default {CertificateSecretName}", namespacedName, _options.DefaultSslCertificate);

        if (!string.Equals(namespacedName.ToString(), _options.DefaultSslCertificate, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Found secret `{NamespacedName}` to use as default certificate for HTTPS traffic", namespacedName);

        var certificate = _certificateHelper.ConvertCertificate(namespacedName, secret);
        if (certificate is null)
        {
            return;
        }

        if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
        {
            _certificateSelector.AddCertificate(namespacedName, certificate);
        }
        else if (eventType == WatchEventType.Deleted)
        {
            _certificateSelector.RemoveCertificate(namespacedName);
        }
    }

    public bool TryGetReconcileData(NamespacedName key, out ReconcileData data)
    {
        return Namespace(key.Namespace).TryLookup(key, out data);
    }

    public void GetKeys(List<NamespacedName> keys)
    {
        lock (_sync)
        {
            foreach (var (ns, cache) in _namespaceCaches)
            {
                cache.GetKeys(ns, keys);
            }
        }
    }

    public IEnumerable<IngressData> GetIngresses()
    {
        var ingresses = new List<IngressData>();

        lock (_sync)
        {
            foreach (var ns in _namespaceCaches)
            {
                ingresses.AddRange(ns.Value.GetIngresses().Where(IsYarpIngress));
            }
        }

        return ingresses;
    }

    private bool IsYarpIngress(IngressData ingress)
    {
        if (ingress.Spec.IngressClassName is null)
        {
            return _isDefaultController;
        }

        lock (_sync)
        {
            return _ingressClassData.ContainsKey(ingress.Spec.IngressClassName);
        }
    }

    private NamespaceCache Namespace(string key)
    {
        lock (_sync)
        {
            if (!_namespaceCaches.TryGetValue(key, out var value))
            {
                value = new NamespaceCache();
                _namespaceCaches.Add(key, value);
            }
            return value;
        }
    }
}
