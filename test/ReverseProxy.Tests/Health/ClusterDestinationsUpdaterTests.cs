// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health.Tests;

public class ClusterDestinationsUpdaterTests
{
    [Fact]
    public void UpdateAllDestinations_UseDestinationsCollectionAsSource()
    {
        var cluster = GetCluster("policy1");
        var destination0 = cluster.Destinations.GetOrAdd("d0", id => new DestinationState(id));
        var destination1 = cluster.Destinations.GetOrAdd("d1", id => new DestinationState(id));
        var destination2 = cluster.Destinations.GetOrAdd("d2", id => new DestinationState(id));
        var expectedAll = new[] { destination0, destination1, destination2 };
        var expectedAvailable = new[] { destination0, destination2 };
        var policy0 = new StubPolicy("policy0", destination1);
        var policy1 = new StubPolicy("policy1", destination1);
        var updater = new ClusterDestinationsUpdater(new[] { policy0, policy1 });

        updater.UpdateAllDestinations(cluster);

        AssertEquals(expectedAll, cluster.DestinationsState.AllDestinations);
        AssertEquals(expectedAvailable, cluster.DestinationsState.AvailableDestinations);

        Assert.False(policy0.IsCalled);
        Assert.Null(policy0.TakenDestinations);
        Assert.True(policy1.IsCalled);
        AssertEquals(expectedAll, policy1.TakenDestinations);
    }

    [Fact]
    public void UpdateAvailableDestinations_UseAllDestinationsAsSource()
    {
        var cluster = GetCluster("policy1");
        var allDestinations = new[] { new DestinationState("d0"), new DestinationState("d1"), new DestinationState("d2") };
        cluster.DestinationsState = new ClusterDestinationsState(allDestinations, new[] { allDestinations[0], allDestinations[1] });
        var expectedAvailable = new[] { allDestinations[0], allDestinations[2] };
        var policy0 = new StubPolicy("policy0", allDestinations[1]);
        var policy1 = new StubPolicy("policy1", allDestinations[1]);
        var updater = new ClusterDestinationsUpdater(new[] { policy0, policy1 });

        updater.UpdateAvailableDestinations(cluster);

        Assert.Empty(cluster.Destinations);
        AssertEquals(allDestinations, cluster.DestinationsState.AllDestinations);
        AssertEquals(expectedAvailable, cluster.DestinationsState.AvailableDestinations);

        Assert.False(policy0.IsCalled);
        Assert.Null(policy0.TakenDestinations);
        Assert.True(policy1.IsCalled);
        AssertEquals(allDestinations, policy1.TakenDestinations);
    }

    private static void AssertEquals(IEnumerable<DestinationState> actual, IEnumerable<DestinationState> expected)
    {
        Assert.Equal(actual.OrderBy(d => d.DestinationId).Select(d => d.DestinationId), expected.OrderBy(d => d.DestinationId).Select(d => d.DestinationId));
    }

    private static ClusterState GetCluster(string policyName)
    {
        var cluster = new ClusterState("cluster1")
        {
            Model = new ClusterModel(
                new ClusterConfig
                {
                    ClusterId = "cluster1",
                    HealthCheck = new HealthCheckConfig { AvailableDestinationsPolicy = policyName }
                },
            httpClient: new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object))
        };

        return cluster;
    }

    private class StubPolicy : IAvailableDestinationsPolicy
    {
        private readonly DestinationState _skipDestination;

        public bool IsCalled { get; private set; }

        public IReadOnlyList<DestinationState> TakenDestinations { get; private set; }

        public StubPolicy(string name, DestinationState skipDestination)
        {
            Name = name;
            _skipDestination = skipDestination;
        }

        public string Name { get; }

        public IReadOnlyList<DestinationState> GetAvailableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
        {
            IsCalled = true;
            TakenDestinations = allDestinations;
            return allDestinations.Where(p => p != _skipDestination).ToArray();
        }
    }
}
