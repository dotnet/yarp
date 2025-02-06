// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Route criteria for a query parameter that must be present on the incoming request.
/// </summary>
public sealed record RouteQueryParameter
{
    /// <summary>
    /// Name of the query parameter to look for.
    /// This field is case insensitive and required.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// A collection of acceptable query parameter values used during routing.
    /// </summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>
    /// Specifies how query parameter values should be compared (e.g. exact matches Vs. contains).
    /// Defaults to <see cref="QueryParameterMatchMode.Exact"/>.
    /// </summary>
    public QueryParameterMatchMode Mode { get; init; }

    /// <summary>
    /// Specifies whether query parameter value comparisons should ignore case.
    /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
    /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool IsCaseSensitive { get; init; }

    public bool Equals(RouteQueryParameter? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && Mode == other.Mode
            && IsCaseSensitive == other.IsCaseSensitive
            && (IsCaseSensitive
                ? CaseSensitiveEqualHelper.Equals(Values, other.Values)
                : CaseInsensitiveEqualHelper.Equals(Values, other.Values));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Name?.GetHashCode(StringComparison.OrdinalIgnoreCase),
            Mode,
            IsCaseSensitive,
            IsCaseSensitive
                ? CaseSensitiveEqualHelper.GetHashCode(Values)
                : CaseInsensitiveEqualHelper.GetHashCode(Values));
    }
}
