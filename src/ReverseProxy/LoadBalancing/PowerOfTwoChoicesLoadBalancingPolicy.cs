// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class PowerOfTwoChoicesLoadBalancingPolicy : ILoadBalancingPolicy
{
    private readonly IRandomFactory _randomFactory;

    public PowerOfTwoChoicesLoadBalancingPolicy(IRandomFactory randomFactory)
    {
        _randomFactory = randomFactory;
    }

    public string Name => LoadBalancingPolicies.PowerOfTwoChoices;

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        var destinationCount = availableDestinations.Count;
        if (destinationCount == 0)
        {
            return null;
        }

        if (destinationCount == 1)
        {
            return availableDestinations[0];
        }

        // Pick two, and then return the least busy. This avoids the effort of searching the whole list, but
        // still avoids overloading a single destination.
        var random = _randomFactory.CreateRandomInstance();
        var firstIndex = random.Next(destinationCount);
        int secondIndex;
        do
        {
            secondIndex = random.Next(destinationCount);
        } while (firstIndex == secondIndex);
        var first = availableDestinations[firstIndex];
        var second = availableDestinations[secondIndex];
        return (first.ConcurrentRequestCount <= second.ConcurrentRequestCount) ? first : second;
    }
}
