// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing;

/// <summary>
/// Load balances across the available destinations.
/// </summary>
internal sealed class LoadBalancingMiddleware
{
    private readonly ILogger _logger;
    private readonly FrozenDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;
    private readonly RequestDelegate _next;

    public LoadBalancingMiddleware(
        RequestDelegate next,
        ILogger<LoadBalancingMiddleware> logger,
        IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loadBalancingPolicies);
        _next = next;
        _logger = logger;
        _loadBalancingPolicies = loadBalancingPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public Task Invoke(HttpContext context)
    {
        var proxyFeature = context.GetReverseProxyFeature();

        var destinations = proxyFeature.AvailableDestinations;
        var destinationCount = destinations.Count;

        DestinationState? destination;

        if (destinationCount == 0)
        {
            destination = null;
        }
        else if (destinationCount == 1)
        {
            destination = destinations[0];
        }
        else
        {
            var currentPolicy = _loadBalancingPolicies.GetRequiredServiceById(proxyFeature.Cluster.Config.LoadBalancingPolicy, LoadBalancingPolicies.PowerOfTwoChoices);
            destination = currentPolicy.PickDestination(context, proxyFeature.Route.Cluster!, destinations);
        }

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
