// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy;

/// <summary>
/// Extension methods for <see cref="IClusterDestinationResolver"/>.
/// </summary>
public static class IClusterDestinationResolverExtensions
{
    /// <summary>
    /// Resolves the destination URI for the given cluster.
    /// </summary>
    /// <param name="resolver">The destination resolver.</param>
    /// <param name="clusterId">The cluster id.</param>
    /// <param name="context">Optional request context used by load balancing policies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected destination URI, or <see langword="null"/> if no destinations are currently available.</returns>
    public static async ValueTask<Uri?> GetDestinationUriAsync(
        this IClusterDestinationResolver resolver,
        string clusterId,
        HttpContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        var destination = await resolver.GetDestinationAsync(clusterId, context, cancellationToken);
        if (destination is null)
        {
            return null;
        }

        return new Uri(destination.Model.Config.Address, UriKind.Absolute);
    }
}
