// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.LoadBalancing;

internal sealed class LoadBalancingDestinationSelector : ILoadBalancingDestinationSelector
{
    private readonly FrozenDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;

    public LoadBalancingDestinationSelector(IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies)
    {
        ArgumentNullException.ThrowIfNull(loadBalancingPolicies);
        _loadBalancingPolicies = loadBalancingPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public DestinationState? PickDestination(
        HttpContext? context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations,
        string? loadBalancingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(availableDestinations);

        var destinationCount = availableDestinations.Count;

        if (destinationCount == 0)
        {
            return null;
        }

        if (destinationCount == 1)
        {
            return availableDestinations[0];
        }

        var currentPolicy = _loadBalancingPolicies.GetRequiredServiceById(
            loadBalancingPolicy ?? cluster.Model.Config.LoadBalancingPolicy,
            LoadBalancingPolicies.PowerOfTwoChoices);
        return currentPolicy.PickDestination(context ?? new DefaultHttpContext(), cluster, availableDestinations);
    }
}
