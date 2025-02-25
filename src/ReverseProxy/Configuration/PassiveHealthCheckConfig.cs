// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Passive health check config.
/// </summary>
public sealed record PassiveHealthCheckConfig
{
    /// <summary>
    /// Whether passive health checks are enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Passive health check policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Destination reactivation period after which an unhealthy destination reverts back to <see cref="DestinationHealth.Unknown"/>.
    /// </summary>
    public TimeSpan? ReactivationPeriod { get; init; }

    public bool Equals(PassiveHealthCheckConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Enabled == other.Enabled
            && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
            && ReactivationPeriod == other.ReactivationPeriod;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Enabled,
            Policy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            ReactivationPeriod);
    }
}
