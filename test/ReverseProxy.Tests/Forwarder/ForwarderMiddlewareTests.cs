// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class ForwarderMiddlewareTests : TestAutoMockBase
{
    [Fact]
    public void Constructor_Works()
    {
        Create<ForwarderMiddleware>();
    }

    [Fact]
    public async Task Invoke_Works()
    {
        var events = TestEventListener.Collect();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");
        httpContext.Request.Path = "/api/test";
        httpContext.Request.QueryString = new QueryString("?a=b&c=d");

        var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
        var httpRequestOptions = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(60),
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        var cluster1 = new ClusterState(clusterId: "cluster1");
        var clusterModel = new ClusterModel(new ClusterConfig() { HttpRequest = httpRequestOptions },
            httpClient);
        var destination1 = cluster1.Destinations.GetOrAdd(
            "destination1",
            id => new DestinationState(id)
            {
                Model = new DestinationModel(new DestinationConfig { Address = "https://localhost:123/a/b/" })
            });
        var routeConfig = new RouteModel(
            config: new RouteConfig() { RouteId = "Route-1" },
            cluster: cluster1,
            transformer: HttpTransformer.Default);

        httpContext.Features.Set<IReverseProxyFeature>(
            new ReverseProxyFeature()
            {
                AvailableDestinations = new List<DestinationState>() { destination1 }.AsReadOnly(),
                Cluster = clusterModel,
                Route = routeConfig,
            });
        httpContext.Features.Set(cluster1);

        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        Mock<IHttpForwarder>()
            .Setup(h => h.SendAsync(
                httpContext,
                It.Is<string>(uri => uri == "https://localhost:123/a/b/"),
                httpClient,
                It.Is<ForwarderRequestConfig>(requestOptions =>
                    requestOptions.ActivityTimeout == httpRequestOptions.ActivityTimeout
                    && requestOptions.Version == httpRequestOptions.Version
                    && requestOptions.VersionPolicy == httpRequestOptions.VersionPolicy),
                It.IsAny<HttpTransformer>()))
            .Returns(
                async () =>
                {
                    tcs1.TrySetResult(true);
                    await tcs2.Task;
                    return ForwarderError.None;
                })
            .Verifiable();

        var sut = Create<ForwarderMiddleware>();

        Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
        Assert.Equal(0, destination1.ConcurrentRequestCount);

        var task = sut.Invoke(httpContext);
        if (task.IsFaulted)
        {
            // Something went wrong, don't hang the test.
            await task;
        }

        Mock<IHttpForwarder>().Verify();

        await tcs1.Task; // Wait until we get to the proxying step.
        Assert.Equal(1, cluster1.ConcurrencyCounter.Value);
        Assert.Equal(1, destination1.ConcurrentRequestCount);

        Assert.Same(destination1, httpContext.GetReverseProxyFeature().ProxiedDestination);

        tcs2.TrySetResult(true);
        await task;
        Assert.Equal(0, cluster1.ConcurrencyCounter.Value);
        Assert.Equal(0, destination1.ConcurrentRequestCount);

        var invoke = Assert.Single(events, e => e.EventName == "ForwarderInvoke");
        Assert.Equal(3, invoke.Payload.Count);
        Assert.Equal(cluster1.ClusterId, (string)invoke.Payload[0]);
        Assert.Equal(routeConfig.Config.RouteId, (string)invoke.Payload[1]);
        Assert.Equal(destination1.DestinationId, (string)invoke.Payload[2]);
    }

    [Fact]
    public async Task NoDestinations_503()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.com");

        var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
        var cluster1 = new ClusterState(clusterId: "cluster1");
        var clusterModel = new ClusterModel(new ClusterConfig(), httpClient);
        var routeConfig = new RouteModel(
            config: new RouteConfig(),
            cluster: cluster1,
            transformer: HttpTransformer.Default);
        httpContext.Features.Set<IReverseProxyFeature>(
            new ReverseProxyFeature()
            {
                AvailableDestinations = Array.Empty<DestinationState>(),
                Cluster = clusterModel,
                Route = routeConfig,
            });

        Mock<IHttpForwarder>()
            .Setup(h => h.SendAsync(
                httpContext,
                It.IsAny<string>(),
                httpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .Returns(() => throw new NotImplementedException());

        var sut = Create<ForwarderMiddleware>();

        Assert.Equal(0, cluster1.ConcurrencyCounter.Value);

        await sut.Invoke(httpContext);
        Assert.Equal(0, cluster1.ConcurrencyCounter.Value);

        Mock<IHttpForwarder>().Verify();
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        var errorFeature = httpContext.Features.Get<IForwarderErrorFeature>();
        Assert.Equal(ForwarderError.NoAvailableDestinations, errorFeature?.Error);
        Assert.Null(errorFeature.Exception);
    }

    [Fact]
    public async Task Invoke_RecordPassiveHealthChecks_RecordsAfterForwardingCompletes()
    {
        var (context, cluster, destination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: true);
        var order = new List<string>();
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy1");
        policy.Setup(p => p.RequestProxied(context, cluster, destination))
            .Callback(() =>
            {
                Assert.Equal(0, cluster.ConcurrencyCounter.Value);
                Assert.Equal(0, destination.ConcurrentRequestCount);
                order.Add("recorded");
            });
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                destination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ReturnsAsync(() =>
            {
                order.Add("forwarded");
                return ForwarderError.None;
            });

        var sut = CreateMiddleware(forwarder.Object, new[] { policy.Object }, recordPassiveHealthChecks: true);

        await sut.Invoke(context);

        Assert.Equal(new[] { "forwarded", "recorded" }, order);
        policy.Verify(p => p.RequestProxied(context, cluster, destination), Times.Once);
    }

    [Fact]
    public async Task Invoke_ForwarderReportsError_RecordsPassiveHealthOutcome()
    {
        var (context, cluster, destination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: true);
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy1");
        policy.Setup(p => p.RequestProxied(context, cluster, destination))
            .Callback(() =>
            {
                var error = context.Features.Get<IForwarderErrorFeature>();
                Assert.Equal(ForwarderError.RequestTimedOut, error?.Error);
            });
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                destination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ReturnsAsync(() =>
            {
                context.Features.Set<IForwarderErrorFeature>(
                    new ForwarderErrorFeature(ForwarderError.RequestTimedOut, new TimeoutException()));
                return ForwarderError.RequestTimedOut;
            });

        var sut = CreateMiddleware(forwarder.Object, new[] { policy.Object }, recordPassiveHealthChecks: true);

        await sut.Invoke(context);

        policy.Verify(p => p.RequestProxied(context, cluster, destination), Times.Once);
    }

    [Fact]
    public async Task Invoke_ForwarderThrows_DoesNotRecordPassiveHealthOutcome()
    {
        var (context, cluster, destination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: true);
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy1");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                destination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ThrowsAsync(new InvalidOperationException("Forwarder failure"));

        var sut = CreateMiddleware(forwarder.Object, new[] { policy.Object }, recordPassiveHealthChecks: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Invoke(context));

        policy.VerifyGet(p => p.Name, Times.Once);
        policy.VerifyNoOtherCalls();
        Assert.Equal(0, cluster.ConcurrencyCounter.Value);
        Assert.Equal(0, destination.ConcurrentRequestCount);
    }

    [Fact]
    public async Task Invoke_RecordPassiveHealthChecksDisabled_DoesNotRecord()
    {
        var (context, _, destination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: false);
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy1");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                destination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ReturnsAsync(ForwarderError.None);

        var sut = CreateMiddleware(forwarder.Object, new[] { policy.Object }, recordPassiveHealthChecks: true);

        await sut.Invoke(context);

        policy.VerifyGet(p => p.Name, Times.Once);
        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_PassiveHealthRecordingNotIntegrated_DoesNotRecord()
    {
        var (context, _, destination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: true);
        var policy = new Mock<IPassiveHealthCheckPolicy>();
        policy.SetupGet(p => p.Name).Returns("policy1");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                destination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ReturnsAsync(ForwarderError.None);

        var sut = CreateMiddleware(forwarder.Object, new[] { policy.Object }, recordPassiveHealthChecks: false);

        await sut.Invoke(context);

        policy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invoke_ReassignedDuringForwarding_RecordsAgainstFinalClusterAndDestination()
    {
        var (context, initialCluster, initialDestination) = CreateContext("cluster1", "policy1", passiveHealthEnabled: true);
        var (_, finalCluster, finalDestination) = CreateContext("cluster2", "policy2", passiveHealthEnabled: true);
        var initialPolicy = new Mock<IPassiveHealthCheckPolicy>();
        initialPolicy.SetupGet(p => p.Name).Returns("policy1");
        var finalPolicy = new Mock<IPassiveHealthCheckPolicy>();
        finalPolicy.SetupGet(p => p.Name).Returns("policy2");
        var forwarder = new Mock<IHttpForwarder>();
        forwarder.Setup(f => f.SendAsync(
                context,
                initialDestination.Model.Config.Address,
                context.GetReverseProxyFeature().Cluster.HttpClient,
                It.IsAny<ForwarderRequestConfig>(),
                It.IsAny<HttpTransformer>()))
            .ReturnsAsync(() =>
            {
                var finalRoute = new RouteModel(new RouteConfig(), finalCluster, HttpTransformer.Default);
                context.ReassignProxyRequest(finalRoute, finalCluster);
                context.GetReverseProxyFeature().ProxiedDestination = finalDestination;
                return ForwarderError.None;
            });

        var sut = CreateMiddleware(
            forwarder.Object,
            new[] { initialPolicy.Object, finalPolicy.Object },
            recordPassiveHealthChecks: true);

        await sut.Invoke(context);

        initialPolicy.VerifyGet(p => p.Name, Times.Once);
        initialPolicy.VerifyNoOtherCalls();
        finalPolicy.Verify(p => p.RequestProxied(context, finalCluster, finalDestination), Times.Once);
        Assert.Equal(0, initialCluster.ConcurrencyCounter.Value);
        Assert.Equal(0, initialDestination.ConcurrentRequestCount);
    }

    private ForwarderMiddleware CreateMiddleware(
        IHttpForwarder forwarder,
        IEnumerable<IPassiveHealthCheckPolicy> policies,
        bool recordPassiveHealthChecks)
    {
        return new ForwarderMiddleware(
            _ => Task.CompletedTask,
            Mock<ILogger<ForwarderMiddleware>>().Object,
            forwarder,
            Mock<IRandomFactory>().Object,
            policies,
            recordPassiveHealthChecks);
    }

    private static (DefaultHttpContext Context, ClusterState Cluster, DestinationState Destination) CreateContext(
        string clusterId,
        string policy,
        bool passiveHealthEnabled)
    {
        var context = new DefaultHttpContext();
        var httpClient = new HttpMessageInvoker(new Mock<HttpMessageHandler>().Object);
        var cluster = new ClusterState(clusterId);
        var clusterModel = new ClusterModel(
            new ClusterConfig
            {
                ClusterId = clusterId,
                HealthCheck = new HealthCheckConfig
                {
                    Passive = new PassiveHealthCheckConfig
                    {
                        Enabled = passiveHealthEnabled,
                        Policy = policy,
                    }
                }
            },
            httpClient);
        cluster.Model = clusterModel;
        var destination = cluster.Destinations.GetOrAdd(
            "destination1",
            id => new DestinationState(id)
            {
                Model = new DestinationModel(new DestinationConfig { Address = "https://localhost:123/" })
            });
        var route = new RouteModel(new RouteConfig { RouteId = "route1" }, cluster, HttpTransformer.Default);
        context.Features.Set<IReverseProxyFeature>(
            new ReverseProxyFeature
            {
                AvailableDestinations = new List<DestinationState> { destination }.AsReadOnly(),
                Cluster = clusterModel,
                Route = route,
            });

        return (context, cluster, destination);
    }
}
