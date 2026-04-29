// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Management;

internal sealed class ClusterDestinationResolver : IClusterDestinationResolver
{
    private readonly IProxyStateLookup _proxyStateLookup;
    private readonly ILoadBalancingDestinationSelector _destinationSelector;

    public ClusterDestinationResolver(
        IProxyStateLookup proxyStateLookup,
        ILoadBalancingDestinationSelector destinationSelector)
    {
        ArgumentNullException.ThrowIfNull(proxyStateLookup);
        ArgumentNullException.ThrowIfNull(destinationSelector);

        _proxyStateLookup = proxyStateLookup;
        _destinationSelector = destinationSelector;
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

        return ValueTask.FromResult(
            _destinationSelector.PickDestination(context, cluster, cluster.DestinationsState.AvailableDestinations));
    }
}
