// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes a destination of a cluster.
/// </summary>
public sealed record DestinationConfig
{
    /// <summary>
    /// Address of this destination. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
    /// This field is required.
    /// </summary>
    public string Address { get; init; } = default!;

    /// <summary>
    /// Endpoint accepting active health check probes. E.g. <c>http://127.0.0.1:1234/</c>.
    /// </summary>
    public string? Health { get; init; }

    /// <summary>
    /// Arbitrary key-value pairs that further describe this destination.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Host header value to pass to this destination.
    /// Used as a fallback if a host is not already specified by request transforms.
    /// </summary>
    public string? Host { get; init; }

    public bool Equals(DestinationConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Health, other.Health, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
            && CaseSensitiveEqualHelper.Equals(Metadata, other.Metadata);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Address?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Health?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Host?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            CaseSensitiveEqualHelper.GetHashCode(Metadata));
    }
}
