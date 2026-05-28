// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client.Tests;

internal class V1DeploymentResourceInformer : ResourceInformer<V1Deployment, V1DeploymentList>
{
    public V1DeploymentResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Deployment> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1DeploymentResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1DeploymentList>> RetrieveResourceListAsync(string resourceVersion = null, ResourceSelector<V1Deployment> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.AppsV1.ListDeploymentForAllNamespacesWithHttpMessagesAsync(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }

    protected override IAsyncEnumerable<(WatchEventType, V1Deployment)> WatchResourceListAsync(string resourceVersion = null, ResourceSelector<V1Deployment> resourceSelector = null,
        Action<Exception> onError = null)
    {
        return Client.AppsV1.WatchListDeploymentForAllNamespacesAsync(resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, onError: onError);
    }
}
