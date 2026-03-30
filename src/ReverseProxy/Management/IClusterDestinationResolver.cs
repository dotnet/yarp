// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy;

/// <summary>
/// Resolves a destination for a cluster using the current runtime state and configured load balancing policy.
/// </summary>
public interface IClusterDestinationResolver
{
    /// <summary>
    /// Resolves a destination for the given cluster.
    /// </summary>
    /// <param name="clusterId">The cluster id.</param>
    /// <param name="context">Optional request context used by load balancing policies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected destination, or <see langword="null"/> if no destinations are currently available.</returns>
    ValueTask<DestinationState?> GetDestinationAsync(
        string clusterId,
        HttpContext? context = null,
        CancellationToken cancellationToken = default);
}
