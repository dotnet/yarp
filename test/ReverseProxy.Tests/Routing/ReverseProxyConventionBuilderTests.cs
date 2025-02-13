// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Builder.Tests;

public class ReverseProxyConventionBuilderTests
{
    [Fact]
    public void ReverseProxyConventionBuilder_Configure_Works()
    {
        var configured = false;

        var conventions = new List<Action<EndpointBuilder>>();
        var builder = new ReverseProxyConventionBuilder(conventions);

        builder.ConfigureEndpoints(builder =>
        {
            configured = true;
        });

        var routeConfig = new RouteConfig();
        var cluster = new ClusterConfig();
        var endpointBuilder = CreateEndpointBuilder(routeConfig, cluster);

        var action = Assert.Single(conventions);
        action(endpointBuilder);

        Assert.True(configured);
    }

    [Fact]
    public void ReverseProxyConventionBuilder_ConfigureWithProxy_Works()
    {
        var configured = false;

        var conventions = new List<Action<EndpointBuilder>>();
        var builder = new ReverseProxyConventionBuilder(conventions);

        builder.ConfigureEndpoints((builder, proxy) =>
        {
            configured = true;
        });

        var routeConfig = new RouteConfig();
        var cluster = new ClusterConfig();
        var endpointBuilder = CreateEndpointBuilder(routeConfig, cluster);

        var action = Assert.Single(conventions);
        action(endpointBuilder);

        Assert.True(configured);
    }

    [Fact]
    public void ReverseProxyConventionBuilder_ConfigureWithProxyAndCluster_Works()
    {
        var configured = false;

        var conventions = new List<Action<EndpointBuilder>>();
        var builder = new ReverseProxyConventionBuilder(conventions);

        builder.ConfigureEndpoints((builder, proxy, cluster) =>
        {
            configured = true;
        });

        var routeConfig = new RouteConfig();
        var cluster = new ClusterConfig();
        var endpointBuilder = CreateEndpointBuilder(routeConfig, cluster);

        var action = Assert.Single(conventions);
        action(endpointBuilder);

        Assert.True(configured);
    }

    private static RouteEndpointBuilder CreateEndpointBuilder(RouteConfig routeConfig, ClusterConfig cluster)
    {
        var endpointBuilder = new RouteEndpointBuilder(context => Task.CompletedTask, RoutePatternFactory.Parse(""), 0);
        var routeModel = new RouteModel(
            routeConfig,
            new ClusterState("cluster-1")
            {
                Model = new ClusterModel(cluster, new HttpMessageInvoker(new HttpClientHandler()))
            },
            HttpTransformer.Default);

        endpointBuilder.Metadata.Add(routeModel);

        return endpointBuilder;
    }
}
