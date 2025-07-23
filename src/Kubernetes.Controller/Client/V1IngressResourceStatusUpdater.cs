// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Linq;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Caching;
using System.Threading;

namespace Yarp.Kubernetes.Controller.Client;

internal sealed class V1IngressResourceStatusUpdater : IIngressResourceStatusUpdater
{
    private readonly IKubernetes _client;
    private readonly YarpOptions _options;
    private readonly ICache _cache;
    private readonly ILogger _logger;

    public V1IngressResourceStatusUpdater(
        IKubernetes client,
        IOptions<YarpOptions> options,
        ICache cache,
        ILogger<V1ServiceResourceInformer> logger)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _options = options.Value;
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task UpdateStatusAsync(CancellationToken cancellationToken)
    {
        var service = await _client.CoreV1.ReadNamespacedServiceStatusAsync(_options.ControllerServiceName, _options.ControllerServiceNamespace, cancellationToken: cancellationToken);
        if (service.Status?.LoadBalancer?.Ingress is { } loadBalancerIngresses)
        {
            var status = new V1IngressStatus(new V1IngressLoadBalancerStatus(loadBalancerIngresses?.Select(ingress => new V1IngressLoadBalancerIngress
            {
                Hostname = ingress.Hostname,
                Ip = ingress.Ip,
                Ports = ingress.Ports?.Select(port => new V1IngressPortStatus(port.Port, port.Protocol, port.Error)).ToArray()
            }).ToArray()));

            var ingresses = _cache.GetIngresses().ToArray();
            foreach (var ingress in ingresses)
            {
                _logger.LogInformation("Updating ingress {IngressClassNamespace}/{IngressClassName} status.", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name);
                var ingressStatus = await _client.NetworkingV1.ReadNamespacedIngressStatusAsync(ingress.Metadata.Name, ingress.Metadata.NamespaceProperty, cancellationToken: cancellationToken);
                ingressStatus.Status = status;
                await _client.NetworkingV1.ReplaceNamespacedIngressStatusAsync(ingressStatus, ingress.Metadata.Name, ingress.Metadata.NamespaceProperty, cancellationToken: cancellationToken);
                _logger.LogInformation("Updated ingress {IngressClassNamespace}/{IngressClassName} status.", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name);
            }
        }
    }
}
