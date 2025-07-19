// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// A request query parameter matcher used during routing.
/// </summary>
internal sealed class QueryParameterMatcher
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public QueryParameterMatcher(string name, IReadOnlyList<string>? values, QueryParameterMatchMode mode, bool isCaseSensitive)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (mode != QueryParameterMatchMode.Exists
            && (values is null || values.Count == 0))
        {
            throw new ArgumentException("Query parameter values must have at least one value.", nameof(values));
        }
        if (mode == QueryParameterMatchMode.Exists && values?.Count > 0)
        {
            throw new ArgumentException($"Query parameter values must not be specified when using '{nameof(QueryParameterMatchMode.Exists)}'.", nameof(values));
        }
        if (values is not null && values.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentNullException(nameof(values), "Query parameter values must not be empty.");
        }

        Name = name;
        Values = values?.ToArray() ?? Array.Empty<string>();
        Mode = mode;
        Comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    /// <summary>
    /// Name of the query parameter to look for.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns a read-only collection of acceptable query parameter values used during routing.
    /// At least one value is required unless <see cref="Mode"/> is set to <see cref="QueryParameterMatchMode.Exists"/>.
    /// </summary>
    public string[] Values { get; }

    /// <summary>
    /// Specifies how query parameter values should be compared (e.g. exact matches Vs. contains).
    /// Defaults to <see cref="QueryParameterMatchMode.Exact"/>.
    /// </summary>
    public QueryParameterMatchMode Mode { get; }

    public StringComparison Comparison { get; }
}
