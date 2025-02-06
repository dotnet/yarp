// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class HealtyAndUnknownDesitnationsPolicyTests
{
    [Fact]
    public void GetAvailableDestinations_HealthChecksEnabled_FilterOutUnhealthy()
    {
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig { Enabled = true },
                Passive = new PassiveHealthCheckConfig { Enabled = true }
            }
        };

        var allDestinations = new[]
        {
            new DestinationState("d1") { Health = { Active = DestinationHealth.Healthy } },
            new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy } },
            new DestinationState("d3") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
            new DestinationState("d4") { Health = { Passive = DestinationHealth.Unhealthy } },
            new DestinationState("d5") { Health = { Passive = DestinationHealth.Healthy } },
            new DestinationState("d6") { Health = { Active = DestinationHealth.Healthy, Passive = DestinationHealth.Unhealthy } },
            new DestinationState("d7") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } },
            new DestinationState("d8")
        };
        var policy = new HealthyAndUnknownDestinationsPolicy();

        var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

        Assert.Equal(3, availableDestinations.Count);
        Assert.Same(allDestinations[0], availableDestinations[0]);
        Assert.Same(allDestinations[4], availableDestinations[1]);
        Assert.Same(allDestinations[7], availableDestinations[2]);
    }

    [Theory]
    [MemberData(nameof(GetDisabledHealthChecksCases))]
    public void GetAvailableDestinations_HealthChecksDisabled_ReturnAll(HealthCheckConfig config)
    {
        var cluster = new ClusterConfig() { ClusterId = "cluster1", HealthCheck = config };
        var allDestinations = new[]
        {
            new DestinationState("d1") { Health = { Active = DestinationHealth.Healthy } },
            new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
            new DestinationState("d3") { Health = { Passive = DestinationHealth.Healthy } },
            new DestinationState("d4"),
            new DestinationState("d5") { Health = { Active = DestinationHealth.Healthy, Passive = DestinationHealth.Unhealthy } },
            new DestinationState("d6") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } }
        };
        var policy = new HealthyAndUnknownDestinationsPolicy();

        var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

        Assert.Equal(6, availableDestinations.Count);
        Assert.Same(allDestinations[0], availableDestinations[0]);
        Assert.Same(allDestinations[1], availableDestinations[1]);
        Assert.Same(allDestinations[2], availableDestinations[2]);
        Assert.Same(allDestinations[3], availableDestinations[3]);
        Assert.Same(allDestinations[4], availableDestinations[4]);
        Assert.Same(allDestinations[5], availableDestinations[5]);
    }

    [Theory]
    [InlineData(true, DestinationHealth.Unhealthy, true, DestinationHealth.Healthy, false)]
    [InlineData(false, DestinationHealth.Unhealthy, true, DestinationHealth.Healthy, true)]
    [InlineData(true, DestinationHealth.Healthy, true, DestinationHealth.Unhealthy, false)]
    [InlineData(true, DestinationHealth.Healthy, false, DestinationHealth.Unhealthy, true)]
    [InlineData(false, DestinationHealth.Unhealthy, false, DestinationHealth.Unhealthy, true)]
    [InlineData(true, DestinationHealth.Unhealthy, true, DestinationHealth.Unhealthy, false)]
    public void GetAvailableDestinations_OneHealthCheckDisabled_UseUnknownState(bool activeEnabled, DestinationHealth active, bool passiveEnabled, DestinationHealth passive, bool isAvailable)
    {
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig { Enabled = activeEnabled },
                Passive = new PassiveHealthCheckConfig { Enabled = passiveEnabled }
            }
        };

        var policy = new HealthyAndUnknownDestinationsPolicy();

        var destination = new DestinationState("d0") { Health = { Active = active, Passive = passive } };
        var availableDestinations = policy.GetAvailalableDestinations(cluster, new[] { destination });

        if (isAvailable)
        {
            Assert.Single(availableDestinations, destination);
        }
        else
        {
            Assert.Empty(availableDestinations);
        }
    }

    public static IEnumerable<object[]> GetDisabledHealthChecksCases()
    {
        yield return new[] { new HealthCheckConfig() };
        yield return new[] { (object)null };
    }
}
