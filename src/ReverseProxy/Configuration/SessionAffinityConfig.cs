// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Session affinity options.
/// </summary>
public sealed record SessionAffinityConfig
{

    /// <summary>
    /// Indicates whether session affinity is enabled.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// The session affinity policy to use.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Strategy for handling a missing destination for an affinitized request.
    /// </summary>
    public string? FailurePolicy { get; init; }

    /// <summary>
    /// Identifies the name of the field where the affinity value is stored.
    /// For the cookie affinity policy this will be the cookie name.
    /// For the header affinity policy this will be the header name.
    /// The policy will give its own default if no value is set.
    /// This value should be unique across clusters to avoid affinity conflicts.
    /// https://github.com/dotnet/yarp/issues/976
    /// This field is required.
    /// </summary>
    public string AffinityKeyName { get; init; } = default!;

    /// <summary>
    /// Configuration of a cookie storing the session affinity key in case
    /// the <see cref="Policy"/> is set to 'Cookie'.
    /// </summary>
    public SessionAffinityCookieConfig? Cookie { get; init; }

    public bool Equals(SessionAffinityConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Enabled == other.Enabled
            && string.Equals(Policy, other.Policy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(FailurePolicy, other.FailurePolicy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(AffinityKeyName, other.AffinityKeyName, StringComparison.Ordinal)
            && Cookie == other.Cookie;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Enabled,
            Policy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            FailurePolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            AffinityKeyName?.GetHashCode(StringComparison.Ordinal),
            Cookie);
    }
}
