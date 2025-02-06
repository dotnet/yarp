// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Updates the cluster's destination collections.
/// </summary>
public interface IClusterDestinationsUpdater
{
    /// <summary>
    /// Updates the cluster's collection of destination available for proxying requests to.
    /// Call this if health state has changed for any destinations.
    /// </summary>
    /// <param name="cluster">The <see cref="ClusterState"/> owing the destinations.</param>
    void UpdateAvailableDestinations(ClusterState cluster);

    /// <summary>
    /// Updates the cluster's collection of all configured destinations.
    /// Call this after destinations have been added, removed, or their configuration changed.
    /// This does not need to be called for state updates like health, use UpdateAvailableDestinations for state updates.
    /// </summary>
    /// <param name="cluster">The <see cref="ClusterState"/> owing the destinations.</param>
    void UpdateAllDestinations(ClusterState cluster);
}
