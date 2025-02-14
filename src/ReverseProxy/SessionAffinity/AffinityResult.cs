// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Affinity resolution result.
/// </summary>
public readonly struct AffinityResult
{
    public IReadOnlyList<DestinationState>? Destinations { get; }

    public AffinityStatus Status { get; }

    public AffinityResult(IReadOnlyList<DestinationState>? destinations, AffinityStatus status)
    {
        Destinations = destinations;
        Status = status;
    }
}
