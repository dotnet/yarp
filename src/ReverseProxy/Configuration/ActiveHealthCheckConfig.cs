// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Active health check config.
/// </summary>
public sealed record ActiveHealthCheckConfig
{
    /// <summary>
    /// Whether active health checks are enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Health probe interval.
    /// </summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Health probe timeout, after which a destination is considered unhealthy.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Active health check policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// HTTP health check endpoint path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Query string to append to the probe, including the leading '?'.
    /// </summary>
    public string? Query { get; init; }

    public bool Equals(ActiveHealthCheckConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Enabled == other.Enabled
            && Interval == other.Interval
            && Timeout == other.Timeout
            && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(Query, other.Query, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Enabled,
            Interval,
            Timeout,
            Policy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Path?.GetHashCode(StringComparison.Ordinal),
            Query?.GetHashCode(StringComparison.Ordinal));
    }
}
