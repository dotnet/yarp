// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

internal sealed class HealthyOrPanicDestinationsPolicy : HealthyAndUnknownDestinationsPolicy
{
    public override string Name => HealthCheckConstants.AvailableDestinations.HealthyOrPanic;

    public override IReadOnlyList<DestinationState> GetAvailableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
    {
        var availableDestinations = base.GetAvailableDestinations(config, allDestinations);
        return availableDestinations.Count > 0 ? availableDestinations : allDestinations;
    }
}
