// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Actively monitors destinations health.
/// </summary>
public interface IActiveHealthCheckMonitor
{
    /// <summary>
    /// Gets a value that determines whether the initial round of active health checks have run, regardless of the results.
    /// </summary>
    /// <returns>
    /// <c>false</c> until the initial round of health check requests has been processed.
    /// <c>true</c> when all the initially configured destinations have been queried, regardless their availability or returned status code.
    /// The property stays <c>true</c> for the rest of the proxy process lifetime.
    /// </returns>
    bool InitialProbeCompleted { get; }

    /// <summary>
    /// Checks health of all clusters' destinations.
    /// </summary>
    /// <param name="clusters">Clusters to check the health of their destinations.</param>
    /// <returns><see cref="Task"/> representing the health check process.</returns>
    Task CheckHealthAsync(IEnumerable<ClusterState> clusters);
}
