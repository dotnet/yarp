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

internal class V1SecretResourceInformer : ResourceInformer<V1Secret, V1SecretList>
{
    public V1SecretResourceInformer(
        IKubernetes client,
        ResourceSelector<V1Secret> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1SecretResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1SecretList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, ResourceSelector<V1Secret> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.CoreV1.ListSecretForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken);
    }
}
