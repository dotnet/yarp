// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.LoadBalancing.Tests;

public class LoadBalancerMiddlewareTests
{
    private static LoadBalancingMiddleware CreateMiddleware(RequestDelegate next, params ILoadBalancingPolicy[] loadBalancingPolicies)
    {
        var logger = new Mock<ILogger<LoadBalancingMiddleware>>();
        logger
            .Setup(l => l.IsEnabled(It.IsAny<LogLevel>()))
            .Returns(true);

        return new LoadBalancingMiddleware(
            next,
            logger.Object,
            loadBalancingPolicies);
    }

    [Fact]
    public void Constructor_Works()
    {
        CreateMiddleware(_ => Task.CompletedTask);
    }

    [Fact]
    public async Task PickDestination_UnsupportedPolicy_Throws()
    {
        const string PolicyName = "NonExistentPolicy";
        var context = CreateContext(PolicyName, new[]
        {
            new DestinationState("destination1"),
            new DestinationState("destination2")
        });

        var sut = CreateMiddleware(_ => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await sut.Invoke(context));
        Assert.Equal($"No {typeof(ILoadBalancingPolicy)} was found for the id '{PolicyName}'. (Parameter 'id')", ex.Message);
    }

    [Fact]
    public async Task PickDestination_SingleDestinations_ShortCircuit()
    {
        var context = CreateContext(LoadBalancingPolicies.FirstAlphabetical, new[]
        {
            new DestinationState("destination1")
        });

        var sut = CreateMiddleware(_ => Task.CompletedTask);

        await sut.Invoke(context);

        var feature = context.Features.Get<IReverseProxyFeature>();
        Assert.NotNull(feature);
        Assert.NotNull(feature.AvailableDestinations);
        Assert.Single(feature.AvailableDestinations);
        Assert.Same("destination1", feature.AvailableDestinations[0].DestinationId);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_Works()
    {
        // Selects the alphabetically first available destination.
        var context = CreateContext(LoadBalancingPolicies.FirstAlphabetical, new[]
        {
            new DestinationState("destination2"),
            new DestinationState("destination1"),
        });

        var sut = CreateMiddleware(_ => Task.CompletedTask, new FirstLoadBalancingPolicy());

        await sut.Invoke(context);

        var feature = context.Features.Get<IReverseProxyFeature>();
        Assert.NotNull(feature);
        Assert.NotNull(feature.AvailableDestinations);
        Assert.Single(feature.AvailableDestinations);
        Assert.Same("destination1", feature.AvailableDestinations[0].DestinationId);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WithoutDestinations_503()
    {
        var context = CreateContext(LoadBalancingPolicies.FirstAlphabetical, Array.Empty<DestinationState>());

        var sut = CreateMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        });

        await sut.Invoke(context);

        var feature = context.Features.Get<IReverseProxyFeature>();
        Assert.NotNull(feature);
        Assert.NotNull(feature.AvailableDestinations);
        Assert.Empty(feature.AvailableDestinations);

        Assert.Equal(503, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ServiceReturnsNoResults_FallThrough()
    {
        const string PolicyName = "CustomPolicy";

        var context = CreateContext(PolicyName, new[]
        {
            new DestinationState("destination1"),
            new DestinationState("destination2")
        });

        var policy = new Mock<ILoadBalancingPolicy>();
        policy
            .Setup(p => p.Name)
            .Returns(PolicyName);
        policy
            .Setup(p => p.PickDestination(It.IsAny<HttpContext>(), It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationState>>()))
            .Returns((DestinationState)null);

        var sut = CreateMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        },
        policy.Object);

        await sut.Invoke(context);

        var feature = context.Features.Get<IReverseProxyFeature>();
        Assert.NotNull(feature);
        Assert.NotNull(feature.AvailableDestinations);
        Assert.Empty(feature.AvailableDestinations);

        Assert.Equal(503, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_NoPolicySpecified_DefaultsToPowerOfTwoChoices()
    {
        var destinations = new[]
        {
            new DestinationState("destination1"),
            new DestinationState("destination2")
        };
        var context = CreateContext(loadBalancingPolicy: null, destinations);

        var policy = new Mock<ILoadBalancingPolicy>();
        policy
            .Setup(p => p.Name)
            .Returns(LoadBalancingPolicies.PowerOfTwoChoices);
        policy
            .Setup(p => p.PickDestination(It.IsAny<HttpContext>(), It.IsAny<ClusterState>(), It.IsAny<IReadOnlyList<DestinationState>>()))
            .Returns((DestinationState)destinations[0]);

        var sut = CreateMiddleware(_ => Task.CompletedTask, policy.Object);

        await sut.Invoke(context);

        policy.Verify(p => p.PickDestination(context, It.IsAny<ClusterState>(), destinations), Times.Once);
    }

    private static HttpContext CreateContext(string loadBalancingPolicy, IReadOnlyList<DestinationState> destinations)
    {
        var cluster = new ClusterState("cluster1")
        {
            Model = new ClusterModel(new ClusterConfig { LoadBalancingPolicy = loadBalancingPolicy },
                new HttpMessageInvoker(new HttpClientHandler()))
        };

        var context = new DefaultHttpContext();

        var route = new RouteModel(new RouteConfig(), cluster, HttpTransformer.Default);
        context.Features.Set<IReverseProxyFeature>(
            new ReverseProxyFeature()
            {
                AvailableDestinations = destinations,
                Route = route,
                Cluster = cluster.Model
            });
        context.Features.Set(cluster);

        var endpoint = new Endpoint(default, new EndpointMetadataCollection(route), string.Empty);
        context.SetEndpoint(endpoint);

        return context;
    }
}
