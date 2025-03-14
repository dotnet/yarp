// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

// Policy marking destinations as available only if their active and passive health states
/// are either 'Healthy' or 'Unknown'/>.
internal class HealthyAndUnknownDestinationsPolicy : IAvailableDestinationsPolicy
{
    public virtual string Name => HealthCheckConstants.AvailableDestinations.HealthyAndUnknown;

    public virtual IReadOnlyList<DestinationState> GetAvailableDestinations(ClusterConfig config, IReadOnlyList<DestinationState> allDestinations)
    {
        var availableDestinations = allDestinations;
        var activeEnabled = (config.HealthCheck?.Active?.Enabled).GetValueOrDefault();
        var passiveEnabled = (config.HealthCheck?.Passive?.Enabled).GetValueOrDefault();

        if (activeEnabled || passiveEnabled)
        {
            availableDestinations = allDestinations.Where(destination =>
            {
                // Only consider the current state if those checks are enabled.
                var healthState = destination.Health;
                var active = activeEnabled ? healthState.Active : DestinationHealth.Unknown;
                var passive = passiveEnabled ? healthState.Passive : DestinationHealth.Unknown;

                return active != DestinationHealth.Unhealthy && passive != DestinationHealth.Unhealthy;
            }).ToList();
        }

        return availableDestinations;
    }
}
