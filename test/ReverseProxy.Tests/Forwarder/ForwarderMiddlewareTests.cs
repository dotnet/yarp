// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

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
}
