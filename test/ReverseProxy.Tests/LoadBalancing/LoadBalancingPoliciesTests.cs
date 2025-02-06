// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing.Tests;

public class LoadBalancingPoliciesTests : TestAutoMockBase
{
    public LoadBalancingPoliciesTests()
    {
        RandomFactory = new TestRandomFactory() { Instance = RandomInstance };
        Provide<IRandomFactory>(RandomFactory);
    }

    private TestRandom RandomInstance { get; set; } = new TestRandom();

    private TestRandomFactory RandomFactory { get; set; }

    [Fact]
    public void PickDestination_FirstWithDestinations_Works()
    {
        var destinations = new[]
        {
            new DestinationState("d1"),
            new DestinationState("d2"),
            new DestinationState("d3")
        };

        var context = new DefaultHttpContext();
        var loadBalancer = Create<FirstLoadBalancingPolicy>();

        for (var i = 0; i < 10; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster: null, availableDestinations: destinations);
            Assert.Same(destinations[0], result);
            result.ConcurrentRequestCount++;
        }
    }

    [Fact]
    public void PickDestination_Random_Works()
    {
        var destinations = new[]
        {
            new DestinationState("d1"),
            new DestinationState("d2"),
            new DestinationState("d3")
        };

        const int Iterations = 10;
        var random = new Random(42);
        RandomInstance.Sequence = Enumerable.Range(0, Iterations).Select(_ => random.Next(destinations.Length)).ToArray();

        var context = new DefaultHttpContext();
        var loadBalancer = Create<RandomLoadBalancingPolicy>();

        for (var i = 0; i < Iterations; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster: null, availableDestinations: destinations);
            Assert.Same(destinations[RandomInstance.Sequence[i]], result);
            result.ConcurrentRequestCount++;
        }
    }

    [Fact]
    public void PickDestination_PowerOfTwoChoices_SkipBusiestConnection()
    {
        var destinations = new[]
        {
            new DestinationState("d1"),
            new DestinationState("d2"),
            new DestinationState("d3")
        };
        destinations[0].ConcurrentRequestCount++;

        const int Iterations = 100;
        var random = new Random(42);
        RandomInstance.Sequence = Enumerable.Range(0, Iterations * Iterations).Select(_ => random.Next(destinations.Length)).ToArray();

        var context = new DefaultHttpContext();
        var loadBalancer = Create<PowerOfTwoChoicesLoadBalancingPolicy>();

        for (var i = 0; i < Iterations; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster: null, availableDestinations: destinations);

            var groupByLoad = destinations.GroupBy(d => d.ConcurrentRequestCount);
            var busiestGroup = groupByLoad.OrderByDescending(g => g.Key).First();
            if (busiestGroup.Count() == 1)
            {
                Assert.True(result.ConcurrentRequestCount < busiestGroup.Key);
            }

            result.ConcurrentRequestCount++;
        }
    }

    [Fact]
    public void PickDestination_PowerOfTwoChoices_LeastLoaded()
    {
        var destinations = new[]
        {
            new DestinationState("d1") {ConcurrentRequestCount = 1000},
            new DestinationState("d2")
        };

        const int Iterations = 100;
        var random = new Random(42);
        RandomInstance.Sequence = Enumerable.Range(0, Iterations * Iterations).Select(_ => random.Next(destinations.Length)).ToArray();

        var context = new DefaultHttpContext();
        var loadBalancer = Create<PowerOfTwoChoicesLoadBalancingPolicy>();

        for (var i = 0; i < Iterations; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster: null, availableDestinations: destinations);
            Assert.Same(destinations[1], result);
        }
    }

    [Fact]
    public void PickDestination_LeastRequests_Works()
    {
        var destinations = new[]
        {
            new DestinationState("d1"),
            new DestinationState("d2"),
            new DestinationState("d3")
        };
        destinations[0].ConcurrentRequestCount++;

        var context = new DefaultHttpContext();
        var loadBalancer = Create<LeastRequestsLoadBalancingPolicy>();

        for (var i = 0; i < 10; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster: null, availableDestinations: destinations);
            Assert.Same(destinations.OrderBy(d => d.ConcurrentRequestCount).First(), result);
            result.ConcurrentRequestCount++;
        }
    }

    [Fact]
    public void PickDestination_RoundRobin_Works()
    {
        var destinations = new[]
        {
            new DestinationState("d1"),
            new DestinationState("d2"),
            new DestinationState("d3")
        };
        destinations[0].ConcurrentRequestCount++;

        var context = new DefaultHttpContext();

        var cluster = new ClusterState("cluster1");
        var routeConfig = new RouteModel(new RouteConfig(), cluster, HttpTransformer.Default);
        var feature = new ReverseProxyFeature()
        {
            Route = routeConfig,
        };
        context.Features.Set<IReverseProxyFeature>(feature);

        var loadBalancer = Create<RoundRobinLoadBalancingPolicy>();

        for (var i = 0; i < 10; i++)
        {
            var result = loadBalancer.PickDestination(context, cluster, availableDestinations: destinations);
            Assert.Same(destinations[i % destinations.Length], result);
            result.ConcurrentRequestCount++;
        }
    }
}
