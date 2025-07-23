// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Periodically invokes specified actions on registered entities.
/// </summary>
/// <remarks>
/// It creates a separate <see cref="Timer"/> for each registration which is considered
/// reasonably efficient because .NET already maintains a process-wide managed timer queue.
/// There are 2 scheduling modes supported: run once and infinite run. In "run once" mode,
/// an entity gets unscheduled after the respective timer fired for the first time whereas
/// in "infinite run" entities get repeatedly rescheduled until either they are explicitly removed
/// or the <see cref="EntityActionScheduler{T}"/> instance is disposed.
/// </remarks>
internal sealed class EntityActionScheduler<T> : IDisposable where T : notnull
{
    private readonly ConcurrentDictionary<T, SchedulerEntry> _entries = new();
    private readonly WeakReference<EntityActionScheduler<T>> _weakThisRef;
    private readonly Func<T, Task> _action;
    private readonly bool _runOnce;
    private readonly TimeProvider _timeProvider;

    private const int NotStarted = 0;
    private const int Started = 1;
    private const int Disposed = 2;
    private int _status;

    public EntityActionScheduler(Func<T, Task> action, bool autoStart, bool runOnce, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _action = action;
        _runOnce = runOnce;
        _timeProvider = timeProvider;
        _status = autoStart ? Started : NotStarted;
        _weakThisRef = new WeakReference<EntityActionScheduler<T>>(this);
    }

    public void Dispose()
    {
        Volatile.Write(ref _status, Disposed);

        foreach (var entry in _entries.Values)
        {
            entry.Dispose();
        }
    }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref _status, Started, NotStarted) != NotStarted)
        {
            return;
        }

        foreach (var entry in _entries.Values)
        {
            entry.EnsureStarted();
        }
    }

    public void ScheduleEntity(T entity, TimeSpan period)
    {
        // Ensure the Timer has a weak reference to this scheduler; otherwise,
        // EntityActionScheduler can be rooted by the Timer implementation.
        var entry = new SchedulerEntry(_weakThisRef, entity, period, _timeProvider);

        if (_entries.TryAdd(entity, entry))
        {
            // Scheduler could have been started while we were adding the new entry.
            // Start timer here to ensure it's not forgotten.
            if (Volatile.Read(ref _status) == Started)
            {
                entry.EnsureStarted();
            }
        }
        else
        {
            entry.Dispose();
        }
    }

    public void ChangePeriod(T entity, TimeSpan newPeriod)
    {
        Debug.Assert(!_runOnce, "Calling ChangePeriod on a RunOnce scheduler may cause the callback to fire twice");

        if (_entries.TryGetValue(entity, out var entry))
        {
            entry.ChangePeriod(newPeriod);
        }
        else
        {
            ScheduleEntity(entity, newPeriod);
        }
    }

    public void UnscheduleEntity(T entity)
    {
        if (_entries.TryRemove(entity, out var entry))
        {
            entry.Dispose();
        }
    }

    public bool IsScheduled(T entity)
    {
        return _entries.ContainsKey(entity);
    }

    private sealed class SchedulerEntry : IDisposable
    {
        private readonly WeakReference<EntityActionScheduler<T>> _scheduler;
        private readonly T _entity;
        private readonly ITimer _timer;
        private TimeSpan _period;
        private bool _timerStarted;
        private bool _runningCallback;

        public SchedulerEntry(WeakReference<EntityActionScheduler<T>> scheduler, T entity, TimeSpan period, TimeProvider timeProvider)
        {
            _scheduler = scheduler;
            _entity = entity;
            _period = period;

            // Don't capture the current ExecutionContext and its AsyncLocals onto the timer causing them to live forever
            var restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _timer = timeProvider.CreateTimer(static s => _ = TimerCallback(s), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        public void ChangePeriod(TimeSpan newPeriod)
        {
            lock (this)
            {
                _period = newPeriod;
                if (_timerStarted && !_runningCallback)
                {
                    SetTimer();
                }
            }
        }

        public void EnsureStarted()
        {
            lock (this)
            {
                if (!_timerStarted)
                {
                    SetTimer();
                }
            }
        }

        private void SetTimer()
        {
            Debug.Assert(Monitor.IsEntered(this));
            Debug.Assert(!_runningCallback);

            _timerStarted = true;

            try
            {
                _timer.Change(_period, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // It can be thrown if the timer has been already disposed.
                // Just suppress it.
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        // Timer.Change is racy as the callback could already be scheduled while we are starting the timer again.
        // Avoid running the callback multiple times concurrently by using the _runningCallback flag.
        private static async Task TimerCallback(object? state)
        {
            var entry = (SchedulerEntry)state!;

            lock (entry)
            {
                if (entry._runningCallback)
                {
                    return;
                }

                entry._runningCallback = true;
            }

            if (!entry._scheduler.TryGetTarget(out var scheduler))
            {
                return;
            }

            var pair = new KeyValuePair<T, SchedulerEntry>(entry._entity, entry);

            if (scheduler._runOnce && scheduler._entries.TryRemove(pair))
            {
                entry.Dispose();
            }

            try
            {
                await scheduler._action(entry._entity);

                if (!scheduler._runOnce && scheduler._entries.Contains(pair))
                {
                    // This entry has not been unscheduled - set the timer again
                    lock (entry)
                    {
                        entry._runningCallback = false;
                        entry.SetTimer();
                    }
                }
            }
            catch (Exception ex)
            {
                // We are running on the ThreadPool, don't propagate exceptions
                Debug.Fail(ex.ToString()); // TODO: Log
                if (scheduler._entries.TryRemove(pair))
                {
                    entry.Dispose();
                }
                return;
            }
        }
    }
}
