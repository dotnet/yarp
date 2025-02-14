// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Stores the current proxy configuration used when processing the request.
/// </summary>
public class ReverseProxyFeature : IReverseProxyFeature
{
    private IReadOnlyList<DestinationState> _availableDestinations = default!;

    /// <inheritdoc/>
    public RouteModel Route { get; init; } = default!;

    /// <inheritdoc/>
    public ClusterModel Cluster { get; set; } = default!;

    /// <inheritdoc/>
    public IReadOnlyList<DestinationState> AllDestinations { get; init; } = default!;

    /// <inheritdoc/>
    public IReadOnlyList<DestinationState> AvailableDestinations
    {
        get => _availableDestinations;
        set => _availableDestinations = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc/>
    public DestinationState? ProxiedDestination { get; set; }
}
