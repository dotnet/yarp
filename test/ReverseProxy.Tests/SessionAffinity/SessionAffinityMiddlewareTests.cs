// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;
using System.Threading;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class SessionAffinityMiddlewareTests
{
    protected const string AffinitizedDestinationName = "dest-B";
    protected readonly ClusterModel ClusterConfig = new ClusterModel(new ClusterConfig
    {
        ClusterId = "cluster-1",
        SessionAffinity = new SessionAffinityConfig
        {
            Enabled = true,
            Policy = "Policy-B",
            FailurePolicy = "Policy-1",
            AffinityKeyName = "Key1"
        }
    },
        new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object));

    [Theory]
    [InlineData(AffinityStatus.AffinityKeyNotSet, null)]
    [InlineData(AffinityStatus.OK, AffinitizedDestinationName)]
    public async Task Invoke_SuccessfulFlow_CallNext(AffinityStatus status, string foundDestinationId)
    {
        var cluster = GetCluster();
        var endpoint = GetEndpoint(cluster);
        DestinationState foundDestination = null;
        if (foundDestinationId is not null)
        {
            cluster.Destinations.TryGetValue(foundDestinationId, out foundDestination);
        }
        var invokedMode = string.Empty;
        const string expectedMode = "Policy-B";
        var policies = RegisterAffinityPolicies(
            true,
            cluster.Destinations.Values.ToList(),
            ("Policy-A", AffinityStatus.DestinationNotFound, (DestinationState)null, (Action<ISessionAffinityPolicy>)(p => throw new InvalidOperationException($"Policy {p.Name} call is not expected."))),
            (expectedMode, status, foundDestination, p => invokedMode = p.Name));
        var nextInvoked = false;
        var middleware = new SessionAffinityMiddleware(c =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            policies.Select(p => p.Object), Array.Empty<IAffinityFailurePolicy>(),
            new Mock<ILogger<SessionAffinityMiddleware>>().Object);
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        var destinationFeature = GetReverseProxyFeature(cluster);
        context.Features.Set(destinationFeature);

        await middleware.Invoke(context);

        Assert.Equal(expectedMode, invokedMode);
        Assert.True(nextInvoked);
        policies[0].VerifyGet(p => p.Name, Times.Once);
        policies[0].VerifyNoOtherCalls();
        policies[1].VerifyAll();

        if (foundDestinationId is not null)
        {
            Assert.Single(destinationFeature.AvailableDestinations);
            Assert.Equal(foundDestinationId, destinationFeature.AvailableDestinations[0].DestinationId);
        }
        else
        {
            Assert.True(cluster.Destinations.Values.SequenceEqual(destinationFeature.AvailableDestinations));
        }
    }

    [Theory]
    [InlineData(AffinityStatus.DestinationNotFound, true)]
    [InlineData(AffinityStatus.DestinationNotFound, false)]
    [InlineData(AffinityStatus.AffinityKeyExtractionFailed, true)]
    [InlineData(AffinityStatus.AffinityKeyExtractionFailed, false)]
    public async Task Invoke_ErrorFlow_CallFailurePolicy(AffinityStatus affinityStatus, bool keepProcessing)
    {
        var cluster = GetCluster();
        var endpoint = GetEndpoint(cluster);
        var policies = RegisterAffinityPolicies(true, cluster.Destinations.Values.ToList(), ("Policy-B", affinityStatus, null, _ => { }));
        var invokedPolicy = string.Empty;
        const string expectedPolicy = "Policy-1";
        var failurePolicies = RegisterFailurePolicies(
            affinityStatus,
            ("Policy-0", false, p => throw new InvalidOperationException($"Policy {p.Name} call is not expected.")),
            (expectedPolicy, keepProcessing, p => invokedPolicy = p.Name));
        var nextInvoked = false;
        var logger = AffinityTestHelper.GetLogger<SessionAffinityMiddleware>();
        var middleware = new SessionAffinityMiddleware(c =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            policies.Select(p => p.Object), failurePolicies.Select(p => p.Object),
            logger.Object);
        var context = new DefaultHttpContext();
        var destinationFeature = GetReverseProxyFeature(cluster);

        context.SetEndpoint(endpoint);
        context.Features.Set(destinationFeature);

        await middleware.Invoke(context);

        Assert.Equal(expectedPolicy, invokedPolicy);
        Assert.Equal(keepProcessing, nextInvoked);
        failurePolicies[0].VerifyGet(p => p.Name, Times.Once);
        failurePolicies[0].VerifyNoOtherCalls();
        failurePolicies[1].VerifyAll();
        if (!keepProcessing)
        {
            logger.Verify(
                l => l.Log(LogLevel.Warning, EventIds.AffinityResolutionFailedForCluster, It.IsAny<It.IsAnyType>(), null, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }
    }

    internal ClusterState GetCluster()
    {
        var cluster = new ClusterState("cluster-1");
        var destinationManager = cluster.Destinations;
        destinationManager.GetOrAdd("dest-A", id => new DestinationState(id));
        destinationManager.GetOrAdd(AffinitizedDestinationName, id => new DestinationState(id));
        destinationManager.GetOrAdd("dest-C", id => new DestinationState(id));
        cluster.Model = ClusterConfig;
        cluster.DestinationsState = new ClusterDestinationsState(destinationManager.Values.ToList(), destinationManager.Values.ToList());
        return cluster;
    }

    internal IReadOnlyList<Mock<ISessionAffinityPolicy>> RegisterAffinityPolicies(
        bool lookupMiddlewareTest,
        IReadOnlyList<DestinationState> expectedDestinations,
        params (string Mode, AffinityStatus? Status, DestinationState Destinations, Action<ISessionAffinityPolicy> Callback)[] prototypes)
    {
        var result = new List<Mock<ISessionAffinityPolicy>>();
        foreach (var (mode, status, destinations, callback) in prototypes)
        {
            var policy = new Mock<ISessionAffinityPolicy>(MockBehavior.Strict);
            policy.SetupGet(p => p.Name).Returns(mode);
            if (lookupMiddlewareTest)
            {
                policy.Setup(p => p.FindAffinitizedDestinationsAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<ClusterState>(),
                    ClusterConfig.Config.SessionAffinity,
                    expectedDestinations,
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<AffinityResult>(new AffinityResult(destinations, status.Value)))
                .Callback(() => callback(policy.Object));
            }
            else
            {
                policy.Setup(p => p.AffinitizeResponseAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<ClusterState>(),
                    ClusterConfig.Config.SessionAffinity,
                    expectedDestinations[0],
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask())
                .Callback(() => callback(policy.Object));
            }
            result.Add(policy);
        }
        return result.AsReadOnly();
    }

    internal IReadOnlyList<Mock<IAffinityFailurePolicy>> RegisterFailurePolicies(AffinityStatus expectedStatus, params (string Name, bool Handled, Action<IAffinityFailurePolicy> Callback)[] prototypes)
    {
        var result = new List<Mock<IAffinityFailurePolicy>>();
        foreach (var (name, handled, callback) in prototypes)
        {
            var policy = new Mock<IAffinityFailurePolicy>(MockBehavior.Strict);
            policy.SetupGet(p => p.Name).Returns(name);
            policy.Setup(p => p.Handle(It.IsAny<HttpContext>(), It.IsAny<ClusterState>(), expectedStatus))
                .ReturnsAsync(handled)
                .Callback(() => callback(policy.Object));
            result.Add(policy);
        }
        return result.AsReadOnly();
    }

    internal IReverseProxyFeature GetReverseProxyFeature(ClusterState cluster)
    {
        return new ReverseProxyFeature()
        {
            AvailableDestinations = cluster.Destinations.Values.ToList(),
            Route = new RouteModel(new RouteConfig(), cluster: cluster, HttpTransformer.Default),
            Cluster = cluster.Model,
        };
    }

    internal Endpoint GetEndpoint(ClusterState cluster)
    {
        var routeConfig = new RouteConfig();
        var routeModel = new RouteModel(routeConfig, cluster, HttpTransformer.Default);
        var endpoint = new Endpoint(default, new EndpointMetadataCollection(routeModel), string.Empty);
        return endpoint;
    }
}
