// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.Tests.Common;

/// <summary>
/// Simulates passage of time, used for testing.
/// </summary>
/// <remarks>
/// This timer doesn't track real time, but instead tracks virtual time.
/// Time only advances when any of the following methods are called:
/// <list type="bullet">
/// <item><see cref="Advance"/></item>
/// <item><see cref="AdvanceTo(TimeSpan)"/></item>
/// </list>
/// </remarks>
public class TestTimeProvider : TimeProvider
{
    private readonly List<TestTimer> _timers = new();

    private TimeSpan _currentTime;

    public int TimerCount => _timers.Count;

    // Mess with the frequency to check for bad assumptions in the code.
    public override long TimestampFrequency => TimeSpan.TicksPerSecond * 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeProvider" /> class.
    /// </summary>
    /// <param name="initialTime">Initial value for current time. Zero if not specified.</param>
    public TestTimeProvider(TimeSpan? initialTime = null)
    {
        _currentTime = initialTime ?? TimeSpan.Zero;
    }

    public TestTimeProvider(DateTimeOffset initialTime)
    {
        _currentTime = initialTime - DateTimeOffset.UnixEpoch;
    }

    /// <summary>
    /// Advances time by the specified amount.
    /// </summary>
    /// <param name="howMuch">How much to advance <see cref="CurrentTime"/> by.</param>
    public void Advance(TimeSpan howMuch)
    {
        AdvanceTo(_currentTime + howMuch);
    }

    /// <summary>
    /// Advances time to the specified point.
    /// </summary>
    /// <param name="targetTime">Advances <see cref="CurrentTime"/> until it equals <paramref name="targetTime"/>.</param>
    public void AdvanceTo(TimeSpan targetTime)
    {
        if (targetTime < _currentTime)
        {
            throw new InvalidOperationException("Time should not flow backwards");
        }

        // We could use this to fire timers, but timers are currently fired manually by tests.

        _currentTime = targetTime;
    }

    public override DateTimeOffset GetUtcNow() => new DateTime(_currentTime.Ticks, DateTimeKind.Utc);

    // Mess with the frequency to check for bad assumptions in the code.
    public override long GetTimestamp() => _currentTime.Ticks * 7;

    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, period);
        var timer = new TestTimer(callback, state, dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    public void FireTimer(int idx)
    {
        _timers[idx].Fire();
    }

    public void FireAllTimers()
    {
        for (var i = 0; i < _timers.Count; i++)
        {
            FireTimer(i);
        }
    }

    public void VerifyTimer(int idx, TimeSpan dueTime)
    {
        Assert.Equal(dueTime, _timers[idx].DueTime);
    }

    public void AssertTimerDisposed(int idx)
    {
        Assert.True(_timers[idx].IsDisposed);
    }

    private sealed class TestTimer : ITimer
    {
        public TestTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            State = state;
            DueTime = dueTime;
            Period = period;
        }

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public TimerCallback Callback { get; private set; }

        public object State { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            Period = period;
            return true;
        }

        public void Fire()
        {
            Callback(State);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return default;
        }
    }
}
