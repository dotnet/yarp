// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Provides session affinity for load-balanced clusters.
/// </summary>
public interface ISessionAffinityPolicy
{
    /// <summary>
    ///  A unique identifier for this session affinity implementation. This will be referenced from config.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Finds <see cref="DestinationState"/> to which the current request is affinitized by the affinity key.
    /// </summary>
    /// <param name="context">Current request's context.</param>
    /// <param name="cluster">Current request's cluster.</param>
    /// <param name="config">Affinity config.</param>
    /// <param name="destinations"><see cref="DestinationState"/>s available for the request.</param>
    /// <returns><see cref="AffinityResult"/> carrying the found affinitized destinations if any and the <see cref="AffinityStatus"/>.</returns>
    AffinityResult FindAffinitizedDestinations(HttpContext context, ClusterState cluster, SessionAffinityConfig config, IReadOnlyList<DestinationState> destinations);

    /// <summary>
    /// Finds <see cref="DestinationState"/> to which the current request is affinitized by the affinity key.
    /// </summary>
    /// <param name="context">Current request's context.</param>
    /// <param name="cluster">Current request's cluster.</param>
    /// <param name="config">Affinity config.</param>
    /// <param name="destinations"><see cref="DestinationState"/>s available for the request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see cref="AffinityResult"/> carrying the found affinitized destinations if any and the <see cref="AffinityStatus"/>.</returns>
    ValueTask<AffinityResult> FindAffinitizedDestinationsAsync(HttpContext context, ClusterState cluster, SessionAffinityConfig config, IReadOnlyList<DestinationState> destinations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new ValueTask<AffinityResult>(FindAffinitizedDestinations(context, cluster, config, destinations));
    }

    /// <summary>
    /// Affinitize the current response to the given <see cref="DestinationState"/> by setting the affinity key extracted from <see cref="DestinationState"/>.
    /// </summary>
    /// <param name="context">Current request's context.</param>
    /// <param name="cluster">Current request's cluster.</param>
    /// <param name="config">Affinity config.</param>
    /// <param name="destination"><see cref="DestinationState"/> to which request is to be affinitized.</param>
    void AffinitizeResponse(HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination);

    /// <summary>
    /// Affinitize the current response to the given <see cref="DestinationState"/> by setting the affinity key extracted from <see cref="DestinationState"/>.
    /// </summary>
    /// <param name="context">Current request's context.</param>
    /// <param name="cluster">Current request's cluster.</param>
    /// <param name="config">Affinity config.</param>
    /// <param name="destination"><see cref="DestinationState"/> to which request is to be affinitized.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask AffinitizeResponseAsync(HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination, CancellationToken cancellationToken)
    {
        AffinitizeResponse(context, cluster, config, destination);
        return default;
    }
}
