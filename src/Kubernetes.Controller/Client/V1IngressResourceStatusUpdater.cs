// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Linq;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Client;

internal class V1IngressResourceStatusUpdater : IIngressResourceStatusUpdater
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
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _client = client;
        _cache = cache;
        _logger = logger;
    }


    public async Task UpdateStatusAsync()
    {
        var service = _client.CoreV1.ReadNamespacedServiceStatus(_options.ControllerServiceName, _options.ControllerServiceNamespace);
        if (service.Status?.LoadBalancer?.Ingress != null)
        {
            var loadBalancerIngresses = service.Status.LoadBalancer.Ingress;
            V1IngressStatus status = new V1IngressStatus(new V1LoadBalancerStatus(loadBalancerIngresses));
            //V1Patch patch = new V1Patch(status, V1Patch.PatchType.ApplyPatch);
            
            var ingresses = _cache.GetIngresses().ToArray();
            foreach (var ingress in ingresses)
            {
                _logger.LogInformation("updating ingress {IngressClassNamespace}/{IngressClassName} status.", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name);
                var s = await _client.NetworkingV1.ReadNamespacedIngressStatusAsync(ingress.Metadata.Name, ingress.Metadata.NamespaceProperty);
                s.Status = status;
                await _client.NetworkingV1.PatchNamespacedIngressStatusAsync(new V1Patch(s), ingress.Metadata.Name, ingress.Metadata.NamespaceProperty);
                _logger.LogInformation("updated ingrees {IngressClassNamespace}/{IngressClassName} status.", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name);
            }
        }
    }
}