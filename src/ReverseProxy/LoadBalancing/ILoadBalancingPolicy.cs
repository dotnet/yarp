// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.LoadBalancing;

/// <summary>
/// Provides a method that applies a load balancing policy
/// to select a destination.
/// </summary>
public interface ILoadBalancingPolicy
{
    /// <summary>
    ///  A unique identifier for this load balancing policy. This will be referenced from config.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Picks a destination to send traffic to.
    /// </summary>
    DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations);
}
