// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Model.Tests;

public class HttpContextFeaturesExtensions
{
    [Fact]
    public void ReassignProxyRequest_Success()
    {
        var client = new HttpMessageInvoker(new SocketsHttpHandler());
        var context = new DefaultHttpContext();
        var d1 = new DestinationState("d1");
        var d2 = new DestinationState("d2");
        var cc1 = new ClusterConfig() { ClusterId = "c1" };
        var cm1 = new ClusterModel(cc1, client);
        var cs1 = new ClusterState("c1") { Model = cm1 };
        var r1 = new RouteModel(new RouteConfig() { RouteId = "r1" }, cs1, HttpTransformer.Empty);
        var feature = new ReverseProxyFeature()
        {
            AllDestinations = d1,
            AvailableDestinations = d1,
            Cluster = cm1,
            Route = r1,
            ProxiedDestination = d1,
        };

        context.Features.Set<IReverseProxyFeature>(feature);

        var cc2 = new ClusterConfig() { ClusterId = "cc2" };
        var cm2 = new ClusterModel(cc2, client);
        var cs2 = new ClusterState("cs2")
        {
            DestinationsState = new ClusterDestinationsState(d2, d2),
            Model = cm2,
        };
        context.ReassignProxyRequest(cs2);

        var newFeature = context.GetReverseProxyFeature();
        Assert.NotSame(feature, newFeature);
        Assert.Same(d2, newFeature.AllDestinations);
        Assert.Same(d2, newFeature.AvailableDestinations);
        Assert.Same(d1, newFeature.ProxiedDestination); // Copied unmodified.
        Assert.Same(cm2, newFeature.Cluster);
        Assert.Same(r1, newFeature.Route);

        // Begin testing ReassignProxyRequest(route, cluster) overload
        var r2 = new RouteModel(new RouteConfig() { RouteId = "r2" }, cs2, HttpTransformer.Empty);
        context.ReassignProxyRequest(r2, cs2);

        var newFeatureOverload = context.GetReverseProxyFeature();
        Assert.NotSame(newFeature, newFeatureOverload);
        Assert.Same(d2, newFeatureOverload.AllDestinations); // Unmodified
        Assert.Same(d2, newFeatureOverload.AvailableDestinations); // Unmodified
        Assert.Same(d1, newFeatureOverload.ProxiedDestination); // Unmodified
        Assert.Same(cm2, newFeatureOverload.Cluster); // Unmodified
        Assert.Same(r2, newFeatureOverload.Route); // Asset route update
    }
}
