// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Yarp.Kubernetes.Controller.Client.Tests;

internal class FlakyWatchResourceInformer : ResourceInformer<V1Pod, V1PodList>
{
    private int _watchCount;

    public FlakyWatchResourceInformer(IHostApplicationLifetime hostApplicationLifetime)
        : base(
            new k8s.Kubernetes(new KubernetesClientConfiguration { Host = "http://localhost" }),
            new ResourceSelector<V1Pod>(null),
            hostApplicationLifetime,
            NullLogger.Instance)
    {
    }

    public TaskCompletionSource WatchRestarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override Task<HttpOperationResponse<V1PodList>> RetrieveResourceListAsync(string resourceVersion = null, ResourceSelector<V1Pod> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HttpOperationResponse<V1PodList>
        {
            Body = new V1PodList
            {
                Metadata = new V1ListMeta { ResourceVersion = "1" },
                Items = new List<V1Pod>(),
            },
        });
    }

    protected override IAsyncEnumerable<(WatchEventType, V1Pod)> WatchResourceListAsync(string resourceVersion = null, ResourceSelector<V1Pod> resourceSelector = null,
        Action<Exception> onError = null)
    {
        return WatchAsync();
    }

    private async IAsyncEnumerable<(WatchEventType, V1Pod)> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (Interlocked.Increment(ref _watchCount) == 1)
        {
            throw new HttpRequestException("Error while copying content to a stream.", new EndOfStreamException());
        }

        WatchRestarted.TrySetResult();

        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }
}
