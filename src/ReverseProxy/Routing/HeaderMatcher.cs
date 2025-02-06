// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// A request header matcher used during routing.
/// </summary>
internal sealed class HeaderMatcher
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public HeaderMatcher(string name, IReadOnlyList<string>? values, HeaderMatchMode mode, bool isCaseSensitive)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A header name is required.", nameof(name));
        }
        if ((mode != HeaderMatchMode.Exists && mode != HeaderMatchMode.NotExists)
            && (values is null || values.Count == 0))
        {
            throw new ArgumentException("Header values must have at least one value.", nameof(values));
        }
        if ((mode == HeaderMatchMode.Exists || mode == HeaderMatchMode.NotExists) && values?.Count > 0)
        {
            throw new ArgumentException($"Header values must not be specified when using '{mode}'.", nameof(values));
        }
        if (values is not null && values.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentNullException(nameof(values), "Header values must be not be empty.");
        }

        Name = name;
        Values = values?.ToArray() ?? Array.Empty<string>();
        Mode = mode;
        Comparison = isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Separator = name.Equals(HeaderNames.Cookie, StringComparison.OrdinalIgnoreCase) ? ';' : ',';
    }

    /// <summary>
    /// Name of the header to look for.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns a read-only collection of acceptable header values used during routing.
    /// At least one value is required unless <see cref="Mode"/> is set to <see cref="HeaderMatchMode.Exists"/>
    /// or <see cref="HeaderMatchMode.NotExists"/>.
    /// </summary>
    public string[] Values { get; }

    /// <summary>
    /// Specifies how header values should be compared (e.g. exact matches Vs. by prefix).
    /// Defaults to <see cref="HeaderMatchMode.ExactHeader"/>.
    /// </summary>
    public HeaderMatchMode Mode { get; }

    public StringComparison Comparison { get; }

    public char Separator { get; }
}
