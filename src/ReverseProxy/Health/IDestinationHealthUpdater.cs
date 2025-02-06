// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Updates destinations' health states when it's requested by a health check policy
/// while taking into account not only the new evaluated value but also the overall current cluster's health state.
/// </summary>
public interface IDestinationHealthUpdater
{
    /// <summary>
    /// Sets the passive health on the given <paramref name="destination"/>.
    /// </summary>
    /// <param name="cluster">Cluster.</param>
    /// <param name="destination">Destination.</param>
    /// <param name="newHealth">New passive health value.</param>
    /// <param name="reactivationPeriod">If <paramref name="newHealth"/> is <see cref="DestinationHealth.Unhealthy"/>,
    /// this parameter specifies a reactivation period after which the destination's <see cref="DestinationHealthState.Passive"/> value
    /// will be reset to <see cref="DestinationHealth.Unknown"/>. Otherwise, it's not used.</param>
    void SetPassive(ClusterState cluster, DestinationState destination, DestinationHealth newHealth, TimeSpan reactivationPeriod);

    /// <summary>
    /// Sets the active health values on the given destinations.
    /// </summary>
    /// <param name="cluster">Cluster.</param>
    /// <param name="newHealthStates">New active health states.</param>
    void SetActive(ClusterState cluster, IEnumerable<NewActiveDestinationHealth> newHealthStates);
}
