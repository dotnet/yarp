// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Model;

public sealed class ClusterDestinationsState
{
    public ClusterDestinationsState(
        IReadOnlyList<DestinationState> allDestinations,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        ArgumentNullException.ThrowIfNull(allDestinations);
        AllDestinations = allDestinations;
        ArgumentNullException.ThrowIfNull(availableDestinations);
        AvailableDestinations = availableDestinations;
    }

    public IReadOnlyList<DestinationState> AllDestinations { get; }

    public IReadOnlyList<DestinationState> AvailableDestinations { get; }
}
