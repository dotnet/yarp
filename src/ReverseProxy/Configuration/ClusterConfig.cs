// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// A cluster is a group of equivalent endpoints and associated policies.
/// </summary>
public sealed record ClusterConfig
{
    /// <summary>
    /// The Id for this cluster. This needs to be globally unique.
    /// This field is required.
    /// </summary>
    public string ClusterId { get; init; } = default!;

    /// <summary>
    /// Load balancing policy.
    /// </summary>
    public string? LoadBalancingPolicy { get; init; }

    /// <summary>
    /// Session affinity config.
    /// </summary>
    public SessionAffinityConfig? SessionAffinity { get; init; }

    /// <summary>
    /// Health checking config.
    /// </summary>
    public HealthCheckConfig? HealthCheck { get; init; }

    /// <summary>
    /// Config for the HTTP client that is used to call destinations in this cluster.
    /// </summary>
    public HttpClientConfig? HttpClient { get; init; }

    /// <summary>
    /// Config for outgoing HTTP requests.
    /// </summary>
    public ForwarderRequestConfig? HttpRequest { get; init; }

    /// <summary>
    /// The set of destinations associated with this cluster.
    /// </summary>
    public IReadOnlyDictionary<string, DestinationConfig>? Destinations { get; init; }

    /// <summary>
    /// Arbitrary key-value pairs that further describe this cluster.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Arbitrary type-value pairs that further extend this cluster.
    /// </summary>
    public IReadOnlyDictionary<Type, IConfigExtension>? Extensions { get; init; }

    public T? GetExtension<T>() where T : IConfigExtension
    {
        if (Extensions?.TryGetValue(typeof(T), out var extension) ?? false)
        {
            return (T)extension;
        }

        return default;
    }

    public bool Equals(ClusterConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return EqualsExcludingDestinations(other)
               && CollectionEqualityHelper.Equals(Destinations, other.Destinations);
    }

    internal bool EqualsExcludingDestinations(ClusterConfig other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(ClusterId, other.ClusterId, StringComparison.OrdinalIgnoreCase)
               && string.Equals(LoadBalancingPolicy, other.LoadBalancingPolicy, StringComparison.OrdinalIgnoreCase)
               // CS0252 warning only shows up in VS https://github.com/dotnet/roslyn/issues/49302
               && SessionAffinity == other.SessionAffinity
               && HealthCheck == other.HealthCheck
               && HttpClient == other.HttpClient
               && HttpRequest == other.HttpRequest
               && CaseSensitiveEqualHelper.Equals(Metadata, other.Metadata)
               && CollectionEqualityHelper.Equals(Extensions, other.Extensions);
    }

    public override int GetHashCode()
    {
        // HashCode.Combine(...) takes only 8 arguments
        var hash = new HashCode();
        hash.Add(ClusterId?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(LoadBalancingPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(SessionAffinity);
        hash.Add(HealthCheck);
        hash.Add(HttpClient);
        hash.Add(HttpRequest);
        hash.Add(CollectionEqualityHelper.GetHashCode(Destinations));
        hash.Add(CaseSensitiveEqualHelper.GetHashCode(Metadata));
        hash.Add(CollectionEqualityHelper.GetHashCode(Extensions));
        return hash.ToHashCode();
    }
}
