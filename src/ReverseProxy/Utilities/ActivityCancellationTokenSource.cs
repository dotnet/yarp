// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class ActivityCancellationTokenSource : CancellationTokenSource
{
    // Avoid paying the cost of updating the timeout timer if doing so won't meaningfully affect
    // the overall timeout duration (default is 100s). This is a trade-off between precision and performance.
    // The exact value is somewhat arbitrary, but should be large enough to avoid most timer updates.
    private const int TimeoutResolutionMs = 20;

    private const int MaxQueueSize = 1024;
    private static readonly ConcurrentQueue<ActivityCancellationTokenSource> _sharedSources = new();
    private static int _count;

    private static readonly Action<object?> _linkedTokenCancelDelegate = static s =>
    {
        var cts = (ActivityCancellationTokenSource)s!;

        // If a cancellation was triggered by a timeout or manual call to Cancel, it's possible that this will
        // cascade into other tokens firing. Avoid incorrectly marking CancelledByLinkedToken in such cases.
        if (!cts.IsCancellationRequested)
        {
            cts.CancelledByLinkedToken = true;
            cts.Cancel(throwOnFirstException: false);
        }
    };

    private int _activityTimeoutMs;
    private uint _lastTimeoutTicks;
    private CancellationTokenRegistration _linkedRegistration1;
    private CancellationTokenRegistration _linkedRegistration2;

    private ActivityCancellationTokenSource() { }

    public bool CancelledByLinkedToken { get; private set; }

    private void StartTimeout()
    {
        _lastTimeoutTicks = (uint)Environment.TickCount;
        CancelAfter(_activityTimeoutMs);
    }

    public void ResetTimeout()
    {
        var currentMs = (uint)Environment.TickCount;
        var elapsedMs = currentMs - _lastTimeoutTicks;

        if (elapsedMs > TimeoutResolutionMs)
        {
            _lastTimeoutTicks = currentMs;
            CancelAfter(_activityTimeoutMs);
        }
    }

    public static ActivityCancellationTokenSource Rent(TimeSpan activityTimeout, CancellationToken linkedToken1 = default, CancellationToken linkedToken2 = default)
    {
        if (_sharedSources.TryDequeue(out var cts))
        {
            Interlocked.Decrement(ref _count);
        }
        else
        {
            cts = new ActivityCancellationTokenSource();
        }

        cts._activityTimeoutMs = (int)activityTimeout.TotalMilliseconds;
        cts._linkedRegistration1 = linkedToken1.UnsafeRegister(_linkedTokenCancelDelegate, cts);
        cts._linkedRegistration2 = linkedToken2.UnsafeRegister(_linkedTokenCancelDelegate, cts);
        cts.StartTimeout();

        return cts;
    }

    public void Return()
    {
        _linkedRegistration1.Dispose();
        _linkedRegistration1 = default;
        _linkedRegistration2.Dispose();
        _linkedRegistration2 = default;

        if (TryReset())
        {
            Debug.Assert(!CancelledByLinkedToken);

            if (Interlocked.Increment(ref _count) <= MaxQueueSize)
            {
                _sharedSources.Enqueue(this);
                return;
            }

            Interlocked.Decrement(ref _count);
        }

        Dispose();
    }
}
