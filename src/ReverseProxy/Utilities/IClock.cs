// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Utilities;

/// <summary>
/// Abstraction over monotonic time providers
/// (Environment.TickCount64, Stopwatch.GetTimestamp, as opposed to DateTime).
/// </summary>
[Obsolete("For testing only. Use TimeProvider instead.")]
public interface IClock
{
    /// <summary>
    /// Gets the current time in UTC as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns></returns>
    DateTimeOffset GetUtcNow();

    /// <summary>
    /// Gets a value that indicates the current tick count measured as milliseconds from an arbitrary reference time.
    /// The default implementation leverages <see cref="Environment.TickCount64"/>.
    /// This is generally more efficient than <see cref="GetStopwatchTime"/>, but provides less precision.
    /// </summary>
    long TickCount { get; }

    /// <summary>
    /// Gets a precise time measurement using <see cref="System.Diagnostics.Stopwatch"/> as the time source.
    /// </summary>
    /// <returns>The time measurement.</returns>
    TimeSpan GetStopwatchTime();

    /// <summary>
    /// Creates a cancellable task that completes after a specified time interval.
    /// This is equivalent to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>,
    /// and facilitates unit tests that use virtual time.
    /// </summary>
    /// <param name="delay">The time span to wait before completing the returned task, or TimeSpan.FromMilliseconds(-1) to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the time delay.</returns>
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a cancellable task that completes after a specified time interval.
    /// This is equivalent to <see cref="Task.Delay(int, CancellationToken)"/>,
    /// and facilitates unit tests that use virtual time.
    /// </summary>
    /// <param name="millisecondsDelay">The number of milliseconds to wait before completing the returned task, or -1 to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the time delay.</returns>
    Task Delay(int millisecondsDelay, CancellationToken cancellationToken);
}
