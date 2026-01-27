// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// A factory for creating <see cref="HttpRequestMessage"/>s for active health probes to be sent to destinations.
/// </summary>
public interface IProbingRequestFactory
{
    /// <summary>
    /// Creates a probing request.
    /// </summary>
    /// <param name="cluster">The cluster being probed.</param>
    /// <param name="destination">The destination being probed.</param>
    /// <returns>Probing <see cref="HttpRequestMessage"/>.</returns>
    HttpRequestMessage CreateRequest(ClusterModel cluster, DestinationModel destination);

    /// <summary>
    /// Creates a probing request.
    /// </summary>
    /// <param name="cluster">The cluster being probed.</param>
    /// <param name="destination">The destination being probed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Probing <see cref="HttpRequestMessage"/>.</returns>
    ValueTask<HttpRequestMessage> CreateRequestAsync(ClusterState cluster, DestinationState destination, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(CreateRequest(cluster.Model, destination.Model));
}
