// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for fetching proxy configuration from the current HttpContext.
/// </summary>
public static class HttpContextFeaturesExtensions
{
    /// <summary>
    /// Retrieves the <see cref="RouteModel"/> instance associated with the current request.
    /// </summary>
    public static RouteModel GetRouteModel(this HttpContext context)
    {
        var proxyFeature = context.GetReverseProxyFeature();

        var route = proxyFeature.Route
            ?? throw new InvalidOperationException($"The {typeof(IReverseProxyFeature).FullName} is missing the {typeof(RouteModel).FullName}.");

        return route;
    }

    /// <summary>
    /// Retrieves the <see cref="IReverseProxyFeature"/> instance associated with the current request.
    /// </summary>
    public static IReverseProxyFeature GetReverseProxyFeature(this HttpContext context)
    {
        return context.Features.Get<IReverseProxyFeature>() ?? throw new InvalidOperationException($"{typeof(IReverseProxyFeature).FullName} is missing.");
    }

    /// <summary>
    /// Retrieves the <see cref="IForwarderErrorFeature"/> instance associated with the current request, if any.
    /// </summary>
    public static IForwarderErrorFeature? GetForwarderErrorFeature(this HttpContext context)
    {
        return context.Features.Get<IForwarderErrorFeature>();
    }

    // Compare to ProxyPipelineInitializerMiddleware
    /// <summary>
    /// Replaces the assigned cluster and destinations in <see cref="IReverseProxyFeature"/> with the new <see cref="ClusterState"/>,
    /// causing the request to be sent to the new cluster instead.
    /// </summary>
    public static void ReassignProxyRequest(this HttpContext context, ClusterState cluster)
    {
        var oldFeature = context.GetReverseProxyFeature();
        var destinations = cluster.DestinationsState;
        var newFeature = new ReverseProxyFeature()
        {
            Route = oldFeature.Route,
            Cluster = cluster.Model,
            AllDestinations = destinations.AllDestinations,
            AvailableDestinations = destinations.AvailableDestinations,
            ProxiedDestination = oldFeature.ProxiedDestination,
        };
        context.Features.Set<IReverseProxyFeature>(newFeature);
    }

    // ReassignProxyRequest overload to also replace the route when updating IReverseProxyFeature
    /// <summary>
    /// Replaces the assigned route, cluster, and destinations in <see cref="IReverseProxyFeature"/> with the new <see cref="RouteModel"/>
    /// and new <see cref="ClusterState"/>, causing the request to be sent using the new route to the new cluster.
    /// </summary>
    public static void ReassignProxyRequest(this HttpContext context, RouteModel route, ClusterState cluster)
    {
        var oldFeature = context.GetReverseProxyFeature();
        var destinations = cluster.DestinationsState;
        var newFeature = new ReverseProxyFeature()
        {
            Route = route,
            Cluster = cluster.Model,
            AllDestinations = destinations.AllDestinations,
            AvailableDestinations = destinations.AvailableDestinations,
            ProxiedDestination = oldFeature.ProxiedDestination,
        };
        context.Features.Set<IReverseProxyFeature>(newFeature);
    }
}
