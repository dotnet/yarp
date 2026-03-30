// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Management;

internal sealed class ClusterDestinationResolver : IClusterDestinationResolver
{
    private readonly IProxyStateLookup _proxyStateLookup;
    private readonly FrozenDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;

    public ClusterDestinationResolver(
        IProxyStateLookup proxyStateLookup,
        IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies)
    {
        ArgumentNullException.ThrowIfNull(proxyStateLookup);
        ArgumentNullException.ThrowIfNull(loadBalancingPolicies);

        _proxyStateLookup = proxyStateLookup;
        _loadBalancingPolicies = loadBalancingPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public ValueTask<DestinationState?> GetDestinationAsync(
        string clusterId,
        HttpContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clusterId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_proxyStateLookup.TryGetCluster(clusterId, out var cluster))
        {
            throw new KeyNotFoundException($"No cluster was found for the id '{clusterId}'.");
        }

        var destinations = cluster.DestinationsState.AvailableDestinations;
        DestinationState? destination;

        if (destinations.Count == 0)
        {
            destination = null;
        }
        else if (destinations.Count == 1)
        {
            destination = destinations[0];
        }
        else
        {
            var policy = _loadBalancingPolicies.GetRequiredServiceById(
                cluster.Model.Config.LoadBalancingPolicy,
                LoadBalancingPolicies.PowerOfTwoChoices);
            destination = policy.PickDestination(context ?? new DefaultHttpContext(), cluster, destinations);
        }

        return ValueTask.FromResult(destination);
    }
}
