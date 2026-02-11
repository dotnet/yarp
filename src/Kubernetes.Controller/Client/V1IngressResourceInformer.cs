// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

internal class V1IngressResourceInformer : ResourceInformer<V1Ingress, V1IngressList>
{
    public V1IngressResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Ingress> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1IngressResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1IngressList>> RetrieveResourceListAsync(string resourceVersion = null, ResourceSelector<V1Ingress> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.NetworkingV1.ListIngressForAllNamespacesWithHttpMessagesAsync(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }

    protected override Watcher<V1Ingress> WatchResourceListAsync(string resourceVersion = null, ResourceSelector<V1Ingress> resourceSelector = null, Action<WatchEventType, V1Ingress> onEvent = null, Action<Exception> onError = null, Action onClosed = null)
    {
        return Client.NetworkingV1.WatchListIngressForAllNamespaces(resourceVersion: resourceVersion,
            fieldSelector: resourceSelector?.FieldSelector, onEvent: onEvent, onError: onError, onClosed: onClosed);
    }
}
