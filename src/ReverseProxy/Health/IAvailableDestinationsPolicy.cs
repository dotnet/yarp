// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Policy evaluating which destinations should be available for proxying requests to.
/// </summary>
public interface IAvailableDestinationsPolicy
{
    /// <summary>
    /// Policy name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reviews all given destinations and returns the ones available for proxying requests to.
    /// </summary>
    /// <param name="config">Target cluster.</param>
    /// <param name="allDestinations">All destinations configured for the target cluster.</param>
    /// <returns></returns>
    IReadOnlyList<DestinationState> GetAvailalableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations);
}
