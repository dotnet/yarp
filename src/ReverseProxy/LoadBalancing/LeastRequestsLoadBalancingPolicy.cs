// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class LeastRequestsLoadBalancingPolicy : ILoadBalancingPolicy
{
    public string Name => LoadBalancingPolicies.LeastRequests;

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        var destinationCount = availableDestinations.Count;
        var leastRequestsDestination = availableDestinations[0];
        var leastRequestsCount = leastRequestsDestination.ConcurrentRequestCount;
        for (var i = 1; i < destinationCount; i++)
        {
            var destination = availableDestinations[i];
            var endpointRequestCount = destination.ConcurrentRequestCount;
            if (endpointRequestCount < leastRequestsCount)
            {
                leastRequestsDestination = destination;
                leastRequestsCount = endpointRequestCount;
            }
        }
        return leastRequestsDestination;
    }
}
