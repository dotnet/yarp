// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Describes the matching criteria for a route.
/// </summary>
public sealed record RouteMatch
{
    /// <summary>
    /// Only match requests that use these optional HTTP methods. E.g. GET, POST.
    /// </summary>
    public IReadOnlyList<string>? Methods { get; init; }

    /// <summary>
    /// Only match requests with the given Host header.
    /// Supports wildcards and ports. For unicode host names, do not use punycode.
    /// </summary>
    public IReadOnlyList<string>? Hosts { get; init; }

    /// <summary>
    /// Only match requests with the given Path pattern.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Only match requests that contain all of these query parameters.
    /// </summary>
    public IReadOnlyList<RouteQueryParameter>? QueryParameters { get; init; }

    /// <summary>
    /// Only match requests that contain all of these headers.
    /// </summary>
    public IReadOnlyList<RouteHeader>? Headers { get; init; }

    public bool Equals(RouteMatch? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase)
            && CaseInsensitiveEqualHelper.Equals(Hosts, other.Hosts)
            && CaseInsensitiveEqualHelper.Equals(Methods, other.Methods)
            && CollectionEqualityHelper.Equals(Headers, other.Headers)
            && CollectionEqualityHelper.Equals(QueryParameters, other.QueryParameters);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Path?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            CaseInsensitiveEqualHelper.GetHashCode(Hosts),
            CaseInsensitiveEqualHelper.GetHashCode(Methods),
            CollectionEqualityHelper.GetHashCode(Headers),
            CollectionEqualityHelper.GetHashCode(QueryParameters));
    }
}
