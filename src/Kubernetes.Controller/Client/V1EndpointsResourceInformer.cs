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

internal class V1EndpointsResourceInformer : ResourceInformer<V1Endpoints, V1EndpointsList>
{
    public V1EndpointsResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Endpoints> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1EndpointsResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1EndpointsList>> RetrieveResourceListAsync(string resourceVersion = null, ResourceSelector<V1Endpoints> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.CoreV1.ListEndpointsForAllNamespacesWithHttpMessagesAsync(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }

    protected override Watcher<V1Endpoints> WatchResourceListAsync(string resourceVersion = null, ResourceSelector<V1Endpoints> resourceSelector = null, Action<WatchEventType, V1Endpoints> onEvent = null, Action<Exception> onError = null, Action onClosed = null)
    {
        return Client.CoreV1.WatchListEndpointsForAllNamespaces(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, onEvent: onEvent, onError: onError, onClosed: onClosed);
    }
}
