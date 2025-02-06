// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

internal class V1ServiceResourceInformer : ResourceInformer<V1Service, V1ServiceList>
{
    public V1ServiceResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Service> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1ServiceResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1ServiceList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, ResourceSelector<V1Service> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.CoreV1.ListServiceForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }
}
