// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Health.Tests;

// It uses a real TimerFactory to verify scheduling work E2E.
public class EntityActionSchedulerTests
{
    private readonly TimeSpan Period0 = TimeSpan.FromSeconds(20);
    private readonly TimeSpan Period1 = TimeSpan.FromSeconds(10);

    [Fact]
    public void Schedule_AutoStartEnabledRunOnceDisabled_StartsAutomaticallyAndRunsIndefinitely()
    {
        var entity0 = new Entity { Id = "entity0" };
        var entity1 = new Entity { Id = "entity1" };
        var timeProvider = new TestTimeProvider();
        Entity lastInvokedEntity = null;
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: true, runOnce: false, timeProvider);

        scheduler.ScheduleEntity(entity0, TimeSpan.FromMilliseconds(20000));
        scheduler.ScheduleEntity(entity1, TimeSpan.FromMilliseconds(10000));

        VerifyEntities(scheduler, entity0, entity1);
        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Period0);
        timeProvider.VerifyTimer(1, Period1);

        timeProvider.FireTimer(1);
        Assert.Same(entity1, lastInvokedEntity);

        timeProvider.FireTimer(0);
        Assert.Same(entity0, lastInvokedEntity);

        timeProvider.FireTimer(1);
        Assert.Same(entity1, lastInvokedEntity);

        timeProvider.FireTimer(0);
        Assert.Same(entity0, lastInvokedEntity);

        VerifyEntities(scheduler, entity0, entity1);
        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Period0);
        timeProvider.VerifyTimer(1, Period1);
    }

    [Fact]
    public void Schedule_AutoStartDisabledRunOnceEnabled_StartsManuallyAndRunsEachRegistrationOnlyOnce()
    {
        var entity0 = new Entity { Id = "entity0" };
        var entity1 = new Entity { Id = "entity1" };
        Entity lastInvokedEntity = null;
        var timeProvider = new TestTimeProvider();
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: false, runOnce: true, timeProvider);

        scheduler.ScheduleEntity(entity0, Period0);
        scheduler.ScheduleEntity(entity1, Period1);
        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Timeout.InfiniteTimeSpan);
        timeProvider.VerifyTimer(1, Timeout.InfiniteTimeSpan);

        scheduler.Start();

        VerifyEntities(scheduler, entity0, entity1);
        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Period0);
        timeProvider.VerifyTimer(1, Period1);

        timeProvider.FireTimer(1);

        Assert.Same(entity1, lastInvokedEntity);

        VerifyEntities(scheduler, entity0);

        timeProvider.FireTimer(0);

        Assert.Same(entity0, lastInvokedEntity);

        Assert.False(scheduler.IsScheduled(entity0));
        Assert.False(scheduler.IsScheduled(entity1));
        timeProvider.AssertTimerDisposed(0);
        timeProvider.AssertTimerDisposed(1);
    }

    [Fact]
    public void Unschedule_EntityUnscheduledBeforeFirstCall_CallbackNotInvoked()
    {
        var entity0 = new Entity { Id = "entity0" };
        var entity1 = new Entity { Id = "entity1" };
        Entity lastInvokedEntity = null;
        var timeProvider = new TestTimeProvider();
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: false, runOnce: false, timeProvider);

        scheduler.ScheduleEntity(entity0, Period0);
        scheduler.ScheduleEntity(entity1, Period1);

        VerifyEntities(scheduler, entity0, entity1);
        Assert.Equal(2, timeProvider.TimerCount);
        timeProvider.VerifyTimer(0, Timeout.InfiniteTimeSpan);
        timeProvider.VerifyTimer(1, Timeout.InfiniteTimeSpan);

        scheduler.UnscheduleEntity(entity1);
        VerifyEntities(scheduler, entity0);
        timeProvider.AssertTimerDisposed(1);

        scheduler.Start();

        timeProvider.VerifyTimer(0, Period0);

        timeProvider.FireTimer(0);

        Assert.Same(entity0, lastInvokedEntity);

        VerifyEntities(scheduler, entity0);
    }

    [Fact]
    public void Unschedule_EntityUnscheduledAfterFirstCall_CallbackInvokedOnlyOnce()
    {
        var entity0 = new Entity { Id = "entity0" };
        var entity1 = new Entity { Id = "entity1" };
        Entity lastInvokedEntity = null;
        var timeProvider = new TestTimeProvider();
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: true, runOnce: false, timeProvider);

        scheduler.ScheduleEntity(entity0, Period0);
        scheduler.ScheduleEntity(entity1, Period1);

        VerifyEntities(scheduler, entity0, entity1);

        timeProvider.FireTimer(1);

        Assert.Same(entity1, lastInvokedEntity);

        timeProvider.FireTimer(0);

        Assert.Same(entity0, lastInvokedEntity);

        scheduler.UnscheduleEntity(entity1);
        VerifyEntities(scheduler, entity0);
        timeProvider.AssertTimerDisposed(1);

        timeProvider.FireTimer(0);

        Assert.Same(entity0, lastInvokedEntity);

        VerifyEntities(scheduler, entity0);
    }

    [Fact]
    public void ChangePeriod_PeriodChangedTimerNotStarted_PeriodChangedBeforeFirstCall()
    {
        var entity = new Entity { Id = "entity0" };
        Entity lastInvokedEntity = null;
        var timeProvider = new TestTimeProvider();
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: false, runOnce: false, timeProvider);

        scheduler.ScheduleEntity(entity, Period0);
        timeProvider.VerifyTimer(0, Timeout.InfiniteTimeSpan);

        var newPeriod = Period1;
        scheduler.ChangePeriod(entity, newPeriod);
        timeProvider.VerifyTimer(0, Timeout.InfiniteTimeSpan);

        scheduler.Start();

        timeProvider.VerifyTimer(0, Period1);

        timeProvider.FireTimer(0);

        Assert.Same(entity, lastInvokedEntity);
    }

    [Fact]
    public void ChangePeriod_TimerStartedPeriodChangedAfterFirstCall_PeriodChangedBeforeNextCall()
    {
        var entity = new Entity { Id = "entity0" };
        Entity lastInvokedEntity = null;
        var timeProvider = new TestTimeProvider();
        using var scheduler = new EntityActionScheduler<Entity>(e =>
        {
            lastInvokedEntity = e;
            return Task.CompletedTask;
        }, autoStart: true, runOnce: false, timeProvider);

        scheduler.ScheduleEntity(entity, Period0);
        timeProvider.VerifyTimer(0, Period0);

        timeProvider.FireTimer(0);

        var newPeriod = Period1;
        scheduler.ChangePeriod(entity, newPeriod);

        timeProvider.VerifyTimer(0, Period1);
        Assert.Same(entity, lastInvokedEntity);
    }

    private void VerifyEntities(EntityActionScheduler<Entity> scheduler, params Entity[] entities)
    {
        var actualCount = 0;
        foreach (var entity in entities)
        {
            Assert.True(scheduler.IsScheduled(entity));
            actualCount++;
        }
        Assert.Equal(entities.Length, actualCount);
    }

    private class Entity
    {
        public string Id { get; set; }
    }
}
