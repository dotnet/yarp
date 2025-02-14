// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;

#pragma warning disable CS0169 // The field 'Reservation.limit' is never used

namespace Yarp.Kubernetes.Controller.Rate;

/// <summary>
/// Class Reservation holds information about events that are permitted by a Limiter to happen after a delay.
/// A Reservation may be canceled, which may enable the Limiter to permit additional events.
/// https://github.com/golang/time/blob/master/rate/rate.go#L106.
/// </summary>
public class Reservation
{
    private readonly TimeProvider _timeProvider;
    private readonly Limiter _limiter;
    private readonly Limit _limit;
    private readonly double _tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="Reservation"/> class.
    /// </summary>
    /// <param name="timeProvider">Gets the system time.</param>
    /// <param name="limiter">The limiter.</param>
    /// <param name="ok">if set to <c>true</c> [ok].</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="timeToAct">The time to act.</param>
    /// <param name="limit">The limit.</param>
    public Reservation(
        TimeProvider timeProvider,
        Limiter limiter,
        bool ok,
        double tokens = default,
        DateTimeOffset timeToAct = default,
        Limit limit = default)
    {
        _timeProvider = timeProvider;
        _limiter = limiter;
        Ok = ok;
        _tokens = tokens;
        TimeToAct = timeToAct;
        _limit = limit;
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="Reservation"/> is ok.
    /// </summary>
    /// <value><c>true</c> if ok; otherwise, <c>false</c>.</value>
    public bool Ok { get; }

    /// <summary>
    /// Gets the time to act.
    /// </summary>
    /// <value>The time to act.</value>
    public DateTimeOffset TimeToAct { get; }

    /// <summary>
    /// Delays this instance.
    /// </summary>
    /// <returns>TimeSpanOffset.</returns>
    public TimeSpan Delay()
    {
        return DelayFrom(_timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Delays from.
    /// </summary>
    /// <param name="now">The now.</param>
    /// <returns>TimeSpan.</returns>
    public TimeSpan DelayFrom(DateTimeOffset now)
    {
        // https://github.com/golang/time/blob/master/rate/rate.go#L134
        if (!Ok)
        {
            return TimeSpan.MaxValue;
        }

        var delay = TimeToAct - now;
        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay;
    }
}
