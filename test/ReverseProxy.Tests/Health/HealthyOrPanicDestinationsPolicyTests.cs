// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class HealthyOrPanicDestinationsPolicyTests
{
    [Fact]
    public void GetAvailableDestinations_SomeDestinationsAreHealthy_ReturnOnlyHealthy()
    {
        var cluster = GetClusterConfig();

        var allDestinations = new[]
        {
            new DestinationState("d1") { Health = { Active = DestinationHealth.Healthy } },
            new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy } },
            new DestinationState("d2") { Health = { Passive = DestinationHealth.Healthy } },
            new DestinationState("d4")
        };
        var policy = new HealthyOrPanicDestinationsPolicy();

        var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

        Assert.Equal(3, availableDestinations.Count);
        Assert.Same(allDestinations[0], availableDestinations[0]);
        Assert.Same(allDestinations[2], availableDestinations[1]);
        Assert.Same(allDestinations[3], availableDestinations[2]);
    }

    [Fact]
    public void GetAvailableDestinations_AllDestinationsAreUnhealthy_ReturnAll()
    {
        var cluster = GetClusterConfig();

        var allDestinations = new[]
        {
            new DestinationState("d1") { Health = { Active = DestinationHealth.Unhealthy } },
            new DestinationState("d2") { Health = { Passive = DestinationHealth.Unhealthy } },
            new DestinationState("d2") { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Healthy } },
            new DestinationState("d4")  { Health = { Active = DestinationHealth.Unhealthy, Passive = DestinationHealth.Unhealthy } }
        };
        var policy = new HealthyOrPanicDestinationsPolicy();

        var availableDestinations = policy.GetAvailalableDestinations(cluster, allDestinations);

        Assert.Equal(4, availableDestinations.Count);
        Assert.Same(allDestinations[0], availableDestinations[0]);
        Assert.Same(allDestinations[1], availableDestinations[1]);
        Assert.Same(allDestinations[2], availableDestinations[2]);
        Assert.Same(allDestinations[3], availableDestinations[3]);
    }

    private static ClusterConfig GetClusterConfig()
    {
        return new ClusterConfig()
        {
            ClusterId = "cluster1",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig { Enabled = true },
                Passive = new PassiveHealthCheckConfig { Enabled = true }
            }
        };
    }
}
