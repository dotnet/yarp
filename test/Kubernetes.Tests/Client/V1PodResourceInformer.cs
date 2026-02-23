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

namespace Yarp.Kubernetes.Controller.Client.Tests;

internal class V1PodResourceInformer : ResourceInformer<V1Pod, V1PodList>
{
    public V1PodResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Pod> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1PodResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1PodList>> RetrieveResourceListAsync(string resourceVersion = null, ResourceSelector<V1Pod> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.CoreV1.ListPodForAllNamespacesWithHttpMessagesAsync(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }

    protected override Watcher<V1Pod> WatchResourceListAsync(string resourceVersion = null, ResourceSelector<V1Pod> resourceSelector = null, Action<WatchEventType, V1Pod> onEvent = null, Action<Exception> onError = null, Action onClosed = null)
    {
        return Client.CoreV1.WatchListPodForAllNamespaces(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, onEvent: onEvent, onError: onError, onClosed: onClosed);
    }
}
