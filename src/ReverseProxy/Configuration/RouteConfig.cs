// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes a route that matches incoming requests based on the <see cref="Match"/> criteria
/// and proxies matching requests to the cluster identified by its <see cref="ClusterId"/>.
/// </summary>
public sealed record RouteConfig
{
    /// <summary>
    /// Globally unique identifier of the route.
    /// This field is required.
    /// </summary>
    public string RouteId { get; init; } = default!;

    /// <summary>
    /// Parameters used to match requests.
    /// This field is required.
    /// </summary>
    public RouteMatch Match { get; init; } = default!;

    /// <summary>
    /// Optionally, an order value for this route. Routes with lower numbers take precedence over higher numbers.
    /// </summary>
    public int? Order { get; init; }

    /// <summary>
    /// Gets or sets the cluster that requests matching this route
    /// should be proxied to.
    /// </summary>
    public string? ClusterId { get; init; }

    /// <summary>
    /// The name of the AuthorizationPolicy to apply to this route.
    /// If not set then only the FallbackPolicy will apply.
    /// Set to "Default" to enable authorization with the applications default policy.
    /// Set to "Anonymous" to disable all authorization checks for this route.
    /// </summary>
    public string? AuthorizationPolicy { get; init; }

    /// <summary>
    /// The name of the RateLimiterPolicy to apply to this route.
    /// If not set then only the GlobalLimiter will apply.
    /// Set to "Disable" to disable rate limiting for this route.
    /// Set to "Default" or leave empty to use the global rate limits, if any.
    /// </summary>
    public string? RateLimiterPolicy { get; init; }

    /// <summary>
    /// The name of the OutputCachePolicy to apply to this route.
    /// If not set then only the BasePolicy will apply.
    /// </summary>
    public string? OutputCachePolicy { get; init; }

    /// <summary>
    /// The name of the TimeoutPolicy to apply to this route.
    /// Setting both Timeout and TimeoutPolicy is an error.
    /// If not set then only the system default will apply.
    /// Set to "Disable" to disable timeouts for this route.
    /// Set to "Default" or leave empty to use the system defaults, if any.
    /// </summary>
    public string? TimeoutPolicy { get; init; }

    /// <summary>
    /// The Timeout to apply to this route. This overrides any system defaults.
    /// Setting both Timeout and TimeoutPolicy is an error.
    /// Timeout granularity is limited to milliseconds.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The name of the CorsPolicy to apply to this route.
    /// If not set then the route won't be automatically matched for cors preflight requests.
    /// Set to "Default" to enable cors with the default policy.
    /// Set to "Disable" to refuses cors requests for this route.
    /// </summary>
    public string? CorsPolicy { get; init; }

    /// <summary>
    /// An optional override for how large request bodies can be in bytes. If set, this overrides the server's default (30MB) per request.
    /// Set to '-1' to disable the limit for this route.
    /// Note that this limit applies only to the YARP forwarder middleware, it does not apply when reading the request body from a custom middleware registered via
    /// <see cref="ReverseProxyIEndpointRouteBuilderExtensions.MapReverseProxy(IEndpointRouteBuilder, Action{IReverseProxyApplicationBuilder})"/>.
    /// </summary>
    public long? MaxRequestBodySize { get; init; }

    /// <summary>
    /// Arbitrary key-value pairs that further describe this route.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Parameters used to transform the request and response. See <see cref="Transforms.Builder.ITransformBuilder"/>.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? Transforms { get; init; }

    public bool Equals(RouteConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return Order == other.Order
            && string.Equals(RouteId, other.RouteId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ClusterId, other.ClusterId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(AuthorizationPolicy, other.AuthorizationPolicy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(RateLimiterPolicy, other.RateLimiterPolicy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(OutputCachePolicy, other.OutputCachePolicy, StringComparison.OrdinalIgnoreCase)
            && string.Equals(TimeoutPolicy, other.TimeoutPolicy, StringComparison.OrdinalIgnoreCase)
            && Timeout == other.Timeout
            && string.Equals(CorsPolicy, other.CorsPolicy, StringComparison.OrdinalIgnoreCase)
            && Match == other.Match
            && CaseSensitiveEqualHelper.Equals(Metadata, other.Metadata)
            && CaseSensitiveEqualHelper.Equals(Transforms, other.Transforms);
    }

    public override int GetHashCode()
    {
        // HashCode.Combine(...) takes only 8 arguments
        var hash = new HashCode();
        hash.Add(Order);
        hash.Add(RouteId?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(ClusterId?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(AuthorizationPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(RateLimiterPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(OutputCachePolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(Timeout?.GetHashCode());
        hash.Add(TimeoutPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(CorsPolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase));
        hash.Add(Match);
        hash.Add(CaseSensitiveEqualHelper.GetHashCode(Metadata));
        hash.Add(CaseSensitiveEqualHelper.GetHashCode(Transforms));
        return hash.ToHashCode();
    }
}
