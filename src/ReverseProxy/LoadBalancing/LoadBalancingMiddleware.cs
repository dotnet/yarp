// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.LoadBalancing;

/// <summary>
/// Load balances across the available destinations.
/// </summary>
internal sealed class LoadBalancingMiddleware
{
    private readonly ILogger _logger;
    private readonly ILoadBalancingDestinationSelector _destinationSelector;
    private readonly RequestDelegate _next;

    public LoadBalancingMiddleware(
        RequestDelegate next,
        ILogger<LoadBalancingMiddleware> logger,
        ILoadBalancingDestinationSelector destinationSelector)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(destinationSelector);
        _next = next;
        _logger = logger;
        _destinationSelector = destinationSelector;
    }

    public Task Invoke(HttpContext context)
    {
        var proxyFeature = context.GetReverseProxyFeature();

        var destination = _destinationSelector.PickDestination(
            context,
            proxyFeature.Route.Cluster!,
            proxyFeature.AvailableDestinations,
            proxyFeature.Cluster.Config.LoadBalancingPolicy);

        if (destination is null)
        {
            // We intentionally do not short circuit here, we allow for later middleware to decide how to handle this case.
            Log.NoAvailableDestinations(_logger, proxyFeature.Cluster.Config.ClusterId);
            proxyFeature.AvailableDestinations = Array.Empty<DestinationState>();
        }
        else
        {
            proxyFeature.AvailableDestinations = destination;
        }

        return _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _noAvailableDestinations = LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.NoAvailableDestinations,
            "No available destinations after load balancing for cluster '{clusterId}'.");

        public static void NoAvailableDestinations(ILogger logger, string clusterId)
        {
            _noAvailableDestinations(logger, clusterId, null);
        }
    }
}
