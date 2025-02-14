// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Limits.Tests;

public class LimitsMiddlewareTests
{
    [Fact]
    public void Constructor_Works()
    {
        CreateMiddleware();
    }

    [Fact]
    public async Task MissingFeature_NoOps()
    {
        var context = CreateContext(10, null);

        var sut = CreateMiddleware();

        await sut.Invoke(context);
    }

    [Theory]
    [InlineData(true, null, null, null)]
    [InlineData(true, 10L, null, 10L)]
    [InlineData(true, null, 10L, null)]
    [InlineData(true, 10L, 11L, 10L)]
    [InlineData(false, null, null, null)]
    [InlineData(false, 10L, null, 10L)]
    [InlineData(false, null, 10L, 10L)]
    [InlineData(false, null, -1L, null)]
    [InlineData(false, 10L, -1L, null)]
    [InlineData(false, 10L, 11L, 11L)]
    public async Task Invoke_CombinationsWork(bool readOnly, long? serverLimit, long? routeLimit, long? expected)
    {
        var feature = new FakeBodySizeFeature() { IsReadOnly = readOnly, MaxRequestBodySize = serverLimit };
        var context = CreateContext(routeLimit, feature);

        var sut = CreateMiddleware();

        await sut.Invoke(context);

        Assert.Equal(expected, feature.MaxRequestBodySize);
    }

    private static LimitsMiddleware CreateMiddleware()
    {
        return new LimitsMiddleware(
            _ => Task.CompletedTask,
            NullLogger<LimitsMiddleware>.Instance);
    }

    private static HttpContext CreateContext(long? bodySizeLimit, IHttpMaxRequestBodySizeFeature feature)
    {
        var cluster = new ClusterState("cluster1")
        {
            Model = new ClusterModel(new ClusterConfig(),
                new HttpMessageInvoker(new HttpClientHandler()))
        };

        var context = new DefaultHttpContext();

        var route = new RouteModel(new RouteConfig() { MaxRequestBodySize = bodySizeLimit }, cluster, HttpTransformer.Default);
        context.Features.Set<IReverseProxyFeature>(
            new ReverseProxyFeature()
            {
                Route = route,
                Cluster = cluster.Model
            });
        context.Features.Set(cluster);

        var endpoint = new Endpoint(default, new EndpointMetadataCollection(route), string.Empty);
        context.SetEndpoint(endpoint);

        context.Features.Set(feature);

        return context;
    }

    private class FakeBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; set; }

        public long? MaxRequestBodySize { get; set; }
    }
}
