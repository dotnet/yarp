// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Yarp.Kubernetes.Controller.Services;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// Per-namespace cache data. Implicitly scopes name-based lookups to same namespace. Also
/// intended to make updates faster because cross-reference dictionaries are not cluster-wide.
/// </summary>
public class NamespaceCache
{
    private readonly object _sync = new object();
    private readonly Dictionary<string, ImmutableList<string>> _ingressToServiceNames = new Dictionary<string, ImmutableList<string>>();
    private readonly Dictionary<string, ImmutableList<string>> _serviceToIngressNames = new Dictionary<string, ImmutableList<string>>();
    private readonly Dictionary<string, IngressData> _ingressData = new Dictionary<string, IngressData>();
    private readonly Dictionary<string, ServiceData> _serviceData = new Dictionary<string, ServiceData>();
    private readonly Dictionary<string, Endpoints> _endpointsData = new Dictionary<string, Endpoints>();

    public void Update(WatchEventType eventType, V1Ingress ingress)
    {
        ArgumentNullException.ThrowIfNull(ingress);

        var serviceNames = ImmutableList<string>.Empty;

        if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
        {
            // If the ingress exists, list out the related services
            var spec = ingress.Spec;
            var defaultBackend = spec?.DefaultBackend;
            var defaultService = defaultBackend?.Service;
            if (!string.IsNullOrEmpty(defaultService?.Name))
            {
                serviceNames = serviceNames.Add(defaultService.Name);
            }

            foreach (var rule in spec.Rules ?? Enumerable.Empty<V1IngressRule>())
            {
                var http = rule.Http;
                foreach (var path in http.Paths ?? Enumerable.Empty<V1HTTPIngressPath>())
                {
                    var backend = path.Backend;
                    var service = backend.Service;

                    if (!serviceNames.Contains(service.Name))
                    {
                        serviceNames = serviceNames.Add(service.Name);
                    }
                }
            }
        }

        var ingressName = ingress.Name();
        lock (_sync)
        {
            var serviceNamesPrevious = ImmutableList<string>.Empty;
            if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
            {
                // If the ingress exists then remember details

                _ingressData[ingressName] = new IngressData(ingress);

                if (_ingressToServiceNames.TryGetValue(ingressName, out serviceNamesPrevious))
                {
                    _ingressToServiceNames[ingressName] = serviceNames;
                }
                else
                {
                    serviceNamesPrevious = ImmutableList<string>.Empty;
                    _ingressToServiceNames.Add(ingressName, serviceNames);
                }
            }
            else if (eventType == WatchEventType.Deleted)
            {
                // otherwise clear out details

                _ingressData.Remove(ingressName);

                if (_ingressToServiceNames.TryGetValue(ingressName, out serviceNamesPrevious))
                {
                    _ingressToServiceNames.Remove(ingressName);
                }
            }

            // update cross-reference for new ingress-to-services linkage not previously known
            foreach (var serviceName in serviceNames)
            {
                if (!serviceNamesPrevious.Contains(serviceName))
                {
                    if (_serviceToIngressNames.TryGetValue(serviceName, out var ingressNamesPrevious))
                    {
                        _serviceToIngressNames[serviceName] = _serviceToIngressNames[serviceName].Add(ingressName);
                    }
                    else
                    {
                        _serviceToIngressNames.Add(serviceName, ImmutableList<string>.Empty.Add(ingressName));
                    }
                }
            }

            // remove cross-reference for previous ingress-to-services linkage no longer present
            foreach (var serviceName in serviceNamesPrevious)
            {
                if (!serviceNames.Contains(serviceName))
                {
                    _serviceToIngressNames[serviceName] = _serviceToIngressNames[serviceName].Remove(ingressName);
                }
            }
        }
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Service service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var serviceName = service.Name();
        lock (_sync)
        {
            if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
            {
                _serviceData[serviceName] = new ServiceData(service);
            }
            else if (eventType == WatchEventType.Deleted)
            {
                _serviceData.Remove(serviceName);
            }

            if (_serviceToIngressNames.TryGetValue(serviceName, out var ingressNames))
            {
                return ingressNames;
            }
            else
            {
                return ImmutableList<string>.Empty;
            }
        }
    }

    public void GetKeys(string ns, List<NamespacedName> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        lock (_sync)
        {
            foreach (var name in _ingressData.Keys)
            {
                keys.Add(new NamespacedName(ns, name));
            }
        }
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var serviceName = endpoints.Name();
        lock (_sync)
        {
            if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
            {
                _endpointsData[serviceName] = new Endpoints(endpoints);
            }
            else if (eventType == WatchEventType.Deleted)
            {
                _endpointsData.Remove(serviceName);
            }

            if (_serviceToIngressNames.TryGetValue(serviceName, out var ingressNames))
            {
                return ingressNames;
            }
            else
            {
                return ImmutableList<string>.Empty;
            }
        }
    }

    public IEnumerable<IngressData> GetIngresses()
    {
        return _ingressData.Values;
    }

    public bool IngressExists(V1Ingress ingress)
    {
        return _ingressData.ContainsKey(ingress.Name());
    }

    public bool TryLookup(NamespacedName key, out ReconcileData data)
    {
        var endpointsList = new List<Endpoints>();
        var servicesList = new List<ServiceData>();

        lock (_sync)
        {
            if (!_ingressData.TryGetValue(key.Name, out var ingress))
            {
                data = default;
                return false;
            }

            if (_ingressToServiceNames.TryGetValue(key.Name, out var serviceNames))
            {
                foreach (var serviceName in serviceNames)
                {
                    if (_serviceData.TryGetValue(serviceName, out var serviceData))
                    {
                        servicesList.Add(serviceData);
                    }

                    if (_endpointsData.TryGetValue(serviceName, out var endpoints))
                    {
                        endpointsList.Add(endpoints);
                    }
                }
            }

            if (_serviceData.Count == 0)
            {
                data = default;
                return false;
            }

            data = new ReconcileData(ingress, servicesList, endpointsList);
            return true;
        }
    }
}
