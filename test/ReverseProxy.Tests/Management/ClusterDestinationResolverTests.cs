// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Management;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Tests;

public class ClusterDestinationResolverTests
{
    [Fact]
    public async Task GetDestinationAsync_MissingCluster_Throws()
    {
        var resolver = CreateResolver(Enumerable.Empty<ClusterState>(), new FirstLoadBalancingPolicy());

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await resolver.GetDestinationAsync("missing"));

        Assert.Equal("No cluster was found for the id 'missing'.", ex.Message);
    }

    [Fact]
    public async Task GetDestinationAsync_WithoutAvailableDestinations_ReturnsNull()
    {
        var cluster = CreateCluster("cluster1", loadBalancingPolicy: null, Array.Empty<DestinationState>());
        var resolver = CreateResolver(new[] { cluster }, new FirstLoadBalancingPolicy());

        var destination = await resolver.GetDestinationAsync("cluster1");

        Assert.Null(destination);
    }

    [Fact]
    public async Task GetDestinationAsync_SingleAvailableDestination_ReturnsDestination()
    {
        var destination1 = CreateDestination("destination1", "https://localhost:10001/");
        var cluster = CreateCluster("cluster1", loadBalancingPolicy: null, new[] { destination1 });
        var resolver = CreateResolver(new[] { cluster }, new FirstLoadBalancingPolicy());

        var destination = await resolver.GetDestinationAsync("cluster1");

        Assert.Same(destination1, destination);
    }

    [Fact]
    public async Task GetDestinationAsync_MultipleAvailableDestinations_UsesConfiguredPolicy()
    {
        var destination1 = CreateDestination("destination2", "https://localhost:10002/");
        var destination2 = CreateDestination("destination1", "https://localhost:10001/");
        var cluster = CreateCluster("cluster1", LoadBalancingPolicies.FirstAlphabetical, new[] { destination1, destination2 });
        var resolver = CreateResolver(new[] { cluster }, new FirstLoadBalancingPolicy());

        var destination = await resolver.GetDestinationAsync("cluster1");

        Assert.Same(destination2, destination);
    }

    [Fact]
    public async Task GetDestinationAsync_PassesHttpContextToPolicy()
    {
        var destination1 = CreateDestination("destination1", "https://localhost:10001/");
        var destination2 = CreateDestination("destination2", "https://localhost:10002/");
        var context = new DefaultHttpContext();
        var policy = new ContextAwarePolicy();
        var cluster = CreateCluster("cluster1", policy.Name, new[] { destination1, destination2 });
        var resolver = CreateResolver(new[] { cluster }, policy);

        var destination = await resolver.GetDestinationAsync("cluster1", context);

        Assert.Same(destination2, destination);
        Assert.Same(context, policy.LastContext);
    }

    [Fact]
    public async Task GetDestinationUriAsync_ReturnsDestinationUri()
    {
        var destination1 = CreateDestination("destination1", "https://localhost:10001/base/");
        var cluster = CreateCluster("cluster1", loadBalancingPolicy: null, new[] { destination1 });
        var resolver = CreateResolver(new[] { cluster }, new FirstLoadBalancingPolicy());

        var uri = await resolver.GetDestinationUriAsync("cluster1");

        Assert.Equal(new Uri("https://localhost:10001/base/"), uri);
    }

    [Fact]
    public async Task GetDestinationAsync_CancellationRequested_Throws()
    {
        var destination1 = CreateDestination("destination1", "https://localhost:10001/");
        var cluster = CreateCluster("cluster1", loadBalancingPolicy: null, new[] { destination1 });
        var resolver = CreateResolver(new[] { cluster }, new FirstLoadBalancingPolicy());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await resolver.GetDestinationAsync("cluster1", cancellationToken: cts.Token));
    }

    private static IClusterDestinationResolver CreateResolver(IEnumerable<ClusterState> clusters, params ILoadBalancingPolicy[] policies)
    {
        return new ClusterDestinationResolver(
            new TestProxyStateLookup(clusters),
            new LoadBalancingDestinationSelector(policies));
    }

    private static ClusterState CreateCluster(string clusterId, string? loadBalancingPolicy, IReadOnlyList<DestinationState> destinations)
    {
        var cluster = new ClusterState(clusterId)
        {
            Model = new ClusterModel(
                new ClusterConfig
                {
                    ClusterId = clusterId,
                    LoadBalancingPolicy = loadBalancingPolicy
                },
                new HttpMessageInvoker(new HttpClientHandler())),
            DestinationsState = new ClusterDestinationsState(destinations, destinations)
        };

        foreach (var destination in destinations)
        {
            cluster.Destinations.TryAdd(destination.DestinationId, destination);
        }

        return cluster;
    }

    private static DestinationState CreateDestination(string destinationId, string address)
    {
        return new DestinationState(destinationId)
        {
            Model = new DestinationModel(new DestinationConfig
            {
                Address = address
            })
        };
    }

    private sealed class TestProxyStateLookup : IProxyStateLookup
    {
        private readonly Dictionary<string, ClusterState> _clusters;

        public TestProxyStateLookup(IEnumerable<ClusterState> clusters)
        {
            _clusters = clusters.ToDictionary(cluster => cluster.ClusterId, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<RouteModel> GetRoutes() => Array.Empty<RouteModel>();

        public IEnumerable<ClusterState> GetClusters() => _clusters.Values;

        public bool TryGetRoute(string id, [NotNullWhen(true)] out RouteModel? route)
        {
            route = null;
            return false;
        }

        public bool TryGetCluster(string id, [NotNullWhen(true)] out ClusterState? cluster)
        {
            return _clusters.TryGetValue(id, out cluster);
        }
    }

    private sealed class ContextAwarePolicy : ILoadBalancingPolicy
    {
        public string Name => "ContextAware";

        public HttpContext? LastContext { get; private set; }

        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            LastContext = context;
            return availableDestinations[^1];
        }
    }
}
