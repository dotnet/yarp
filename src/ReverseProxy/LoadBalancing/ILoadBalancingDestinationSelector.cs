// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.LoadBalancing;

internal interface ILoadBalancingDestinationSelector
{
    DestinationState? PickDestination(
        HttpContext? context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations,
        string? loadBalancingPolicy = null);
}
