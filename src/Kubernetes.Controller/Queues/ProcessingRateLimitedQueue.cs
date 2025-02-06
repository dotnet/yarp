// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Rate;

namespace Yarp.Kubernetes.Controller.Queues;

public class ProcessingRateLimitedQueue<TItem> : WorkQueue<TItem>
{
    private readonly Limiter _limiter;

    public ProcessingRateLimitedQueue(double perSecond, int burst)
    {
        _limiter = new Limiter(new Limit(perSecond), burst);
    }

    protected override async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var delay = _limiter.Reserve().Delay();
        await Task.Delay(delay, cancellationToken);
    }
}
