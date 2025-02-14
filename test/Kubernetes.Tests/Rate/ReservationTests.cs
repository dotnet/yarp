// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.Kubernetes.Controller.Rate.Tests;

public class ReservationTests
{
    private readonly DateTimeOffset _startTime = new DateTimeOffset(2020, 10, 14, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public void NotOkayAlwaysReturnsMaxValueDelay()
    {
        var timeProvider = new TestTimeProvider(_startTime);
        var reservation = new Reservation(
            timeProvider: timeProvider,
            limiter: default,
            ok: false);

        var delay1 = reservation.Delay();
        var delayFrom1 = reservation.DelayFrom(timeProvider.GetUtcNow());
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        var delayFrom2 = reservation.DelayFrom(timeProvider.GetUtcNow());

        Assert.Equal(TimeSpan.MaxValue, delay1);
        Assert.Equal(TimeSpan.MaxValue, delayFrom1);
        Assert.Equal(TimeSpan.MaxValue, delay2);
        Assert.Equal(TimeSpan.MaxValue, delayFrom2);
    }

    [Fact]
    public void DelayIsZeroWhenTimeToActIsNowOrEarlier()
    {
        var timeProvider = new TestTimeProvider(_startTime);
        var reservation = new Reservation(
            timeProvider: timeProvider,
            limiter: default,
            ok: true,
            timeToAct: timeProvider.GetUtcNow(),
            limit: default);

        var delay1 = reservation.Delay();
        var delayFrom1 = reservation.DelayFrom(timeProvider.GetUtcNow());
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        var delayFrom2 = reservation.DelayFrom(timeProvider.GetUtcNow());

        Assert.Equal(TimeSpan.Zero, delay1);
        Assert.Equal(TimeSpan.Zero, delayFrom1);
        Assert.Equal(TimeSpan.Zero, delay2);
        Assert.Equal(TimeSpan.Zero, delayFrom2);
    }

    [Fact]
    public void DelayGetsSmallerAsTimePasses()
    {
        var timeProvider = new TestTimeProvider(_startTime);
        var reservation = new Reservation(
            timeProvider: timeProvider,
            limiter: default,
            ok: true,
            timeToAct: timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(5)),
            limit: default);

        var delay1 = reservation.Delay();
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.Delay();
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var delay3 = reservation.Delay();

        Assert.Equal(TimeSpan.FromMinutes(5), delay1);
        Assert.Equal(TimeSpan.FromMinutes(2), delay2);
        Assert.Equal(TimeSpan.Zero, delay3);
    }

    [Fact]
    public void DelayFromNotChangedByTimePassing()
    {
        var timeProvider = new TestTimeProvider(_startTime);
        var reservation = new Reservation(
            timeProvider: timeProvider,
            limiter: default,
            ok: true,
            timeToAct: timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(5)),
            limit: default);

        var twoMinutesPast = timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(2));
        var twoMinutesFuture = timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(2));

        var delay1 = reservation.DelayFrom(timeProvider.GetUtcNow());
        var delayPast1 = reservation.DelayFrom(twoMinutesPast);
        var delayFuture1 = reservation.DelayFrom(twoMinutesFuture);
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var delay2 = reservation.DelayFrom(timeProvider.GetUtcNow());
        var delayPast2 = reservation.DelayFrom(twoMinutesPast);
        var delayFuture2 = reservation.DelayFrom(twoMinutesFuture);

        Assert.Equal(TimeSpan.FromMinutes(5), delay1);
        Assert.Equal(TimeSpan.FromMinutes(7), delayPast1);
        Assert.Equal(TimeSpan.FromMinutes(3), delayFuture1);
        Assert.Equal(TimeSpan.FromMinutes(2), delay2);
        Assert.Equal(TimeSpan.FromMinutes(7), delayPast2);
        Assert.Equal(TimeSpan.FromMinutes(3), delayFuture2);
    }
}
