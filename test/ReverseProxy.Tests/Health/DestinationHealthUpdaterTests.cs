// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class DestinationHealthUpdaterTests
{
    [Fact]
    public async Task SetPassiveAsync_DestinationBecameUnhealthy_SetUnhealthyAndScheduleReactivation()
    {
        var destination = new DestinationState("destination0");
        destination.Health.Active = DestinationHealth.Healthy;
        destination.Health.Passive = DestinationHealth.Healthy;
        var cluster = CreateCluster(passive: true, active: false, destination);
        var timeProvider = new TestTimeProvider();
        var updater = new DestinationHealthUpdater(timeProvider, GetClusterUpdater(), new Mock<ILogger<DestinationHealthUpdater>>().Object);

        await updater.SetPassiveAsync(cluster, destination, DestinationHealth.Unhealthy, TimeSpan.FromSeconds(2));

        timeProvider.VerifyTimer(0, TimeSpan.FromSeconds(2));
        Assert.Empty(cluster.DestinationsState.AvailableDestinations);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
        Assert.Equal(DestinationHealth.Unhealthy, destination.Health.Passive);

        timeProvider.FireAllTimers();
        GC.KeepAlive(updater); // The timer does not keep a strong reference to the scheduler

        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
        Assert.Equal(DestinationHealth.Unknown, destination.Health.Passive);
        Assert.Single(cluster.DestinationsState.AvailableDestinations);
        Assert.Same(destination, cluster.DestinationsState.AvailableDestinations[0]);
        timeProvider.AssertTimerDisposed(0);
    }

    [Fact]
    public async Task SetPassiveAsync_DestinationBecameHealthy_SetNewState()
    {
        var destination = new DestinationState("destination0");
        destination.Health.Active = DestinationHealth.Healthy;
        destination.Health.Passive = DestinationHealth.Unhealthy;
        var cluster = CreateCluster(passive: true, active: false, destination);
        var timeProvider = new TestTimeProvider();
        var updater = new DestinationHealthUpdater(timeProvider, GetClusterUpdater(), new Mock<ILogger<DestinationHealthUpdater>>().Object);

        await updater.SetPassiveAsync(cluster, destination, DestinationHealth.Healthy, TimeSpan.FromSeconds(2));

        Assert.Equal(0, timeProvider.TimerCount);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Passive);
        Assert.Single(cluster.DestinationsState.AvailableDestinations);
        Assert.Same(destination, cluster.DestinationsState.AvailableDestinations[0]);
    }

    [Theory]
    [InlineData(DestinationHealth.Unhealthy)]
    [InlineData(DestinationHealth.Healthy)]
    [InlineData(DestinationHealth.Unknown)]
    public async Task SetPassiveAsync_HealthSateIsNotChanged_DoNothing(DestinationHealth health)
    {
        var destination = new DestinationState("destination0");
        destination.Health.Active = DestinationHealth.Healthy;
        destination.Health.Passive = health;
        var cluster = CreateCluster(passive: true, active: false, destination);
        var timeProvider = new TestTimeProvider();
        var updater = new DestinationHealthUpdater(timeProvider, GetClusterUpdater(), new Mock<ILogger<DestinationHealthUpdater>>().Object);

        await updater.SetPassiveAsync(cluster, destination, health, TimeSpan.FromSeconds(2));

        Assert.Equal(0, timeProvider.TimerCount);
        Assert.Equal(DestinationHealth.Healthy, destination.Health.Active);
        Assert.Equal(health, destination.Health.Passive);
    }

    [Fact]
    public void SetActive_ChangedAndUnchangedHealthStates_SetChangedStates()
    {
        var destination0 = new DestinationState("destination0");
        destination0.Health.Active = DestinationHealth.Healthy;
        destination0.Health.Passive = DestinationHealth.Healthy;
        var destination1 = new DestinationState("destination1");
        destination1.Health.Active = DestinationHealth.Healthy;
        destination1.Health.Passive = DestinationHealth.Healthy;
        var destination2 = new DestinationState("destination2");
        destination2.Health.Active = DestinationHealth.Unhealthy;
        destination2.Health.Passive = DestinationHealth.Healthy;
        var destination3 = new DestinationState("destination3");
        destination3.Health.Active = DestinationHealth.Unhealthy;
        destination3.Health.Passive = DestinationHealth.Healthy;
        var cluster = CreateCluster(passive: false, active: true, destination0, destination1, destination2, destination3);
        var updater = new DestinationHealthUpdater(new Mock<TimeProvider>().Object, GetClusterUpdater(), new Mock<ILogger<DestinationHealthUpdater>>().Object);

        var newHealthStates = new[] {
            new NewActiveDestinationHealth(destination0, DestinationHealth.Unhealthy), new NewActiveDestinationHealth(destination1, DestinationHealth.Healthy),
            new NewActiveDestinationHealth(destination2, DestinationHealth.Unhealthy), new NewActiveDestinationHealth(destination3, DestinationHealth.Healthy)
        };
        updater.SetActive(cluster, newHealthStates);

        foreach (var newHealthState in newHealthStates)
        {
            Assert.Equal(newHealthState.NewActiveHealth, newHealthState.Destination.Health.Active);
            Assert.Equal(DestinationHealth.Healthy, newHealthState.Destination.Health.Passive);
        }

        Assert.Equal(2, cluster.DestinationsState.AvailableDestinations.Count);
        Assert.Contains(cluster.DestinationsState.AvailableDestinations, d => d == destination1);
        Assert.Contains(cluster.DestinationsState.AvailableDestinations, d => d == destination3);
    }

    private static ClusterState CreateCluster(bool passive, bool active, params DestinationState[] destinations)
    {
        var cluster = new ClusterState("cluster0");
        cluster.Model = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = cluster.ClusterId,
                HealthCheck = new HealthCheckConfig()
                {
                    Passive = new PassiveHealthCheckConfig()
                    {
                        Policy = "policy0",
                        Enabled = passive,
                    },
                    Active = new ActiveHealthCheckConfig()
                    {
                        Enabled = active,
                        Policy = "policy1",
                    },
                },
            },
            new HttpMessageInvoker(new HttpClientHandler()));

        foreach (var destination in destinations)
        {
            cluster.Destinations.TryAdd(destination.DestinationId, destination);
        }

        cluster.DestinationsState = new ClusterDestinationsState(destinations, destinations);

        return cluster;
    }

    private IClusterDestinationsUpdater GetClusterUpdater()
    {
        var result = new Mock<IClusterDestinationsUpdater>(MockBehavior.Strict);
        result.Setup(u => u.UpdateAvailableDestinations(It.IsAny<ClusterState>())).Callback((ClusterState c) =>
        {
            var availableDestinations = c.Destinations.Values
                .Where(d => d.Health.Active != DestinationHealth.Unhealthy && d.Health.Passive != DestinationHealth.Unhealthy)
                .ToList();
            c.DestinationsState = new ClusterDestinationsState(c.DestinationsState.AllDestinations, availableDestinations);
        });
        return result.Object;
    }
}
