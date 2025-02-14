// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// All health check config.
/// </summary>
public sealed record HealthCheckConfig
{
    /// <summary>
    /// Passive health check config.
    /// </summary>
    public PassiveHealthCheckConfig? Passive { get; init; }

    /// <summary>
    /// Active health check config.
    /// </summary>
    public ActiveHealthCheckConfig? Active { get; init; }

    /// <summary>
    /// Available destinations policy.
    /// </summary>
    public string? AvailableDestinationsPolicy { get; init; }

    public bool Equals(HealthCheckConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Passive == other.Passive
            && Active == other.Active
            && string.Equals(AvailableDestinationsPolicy, other.AvailableDestinationsPolicy, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Passive,
            Active,
            AvailableDestinationsPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
