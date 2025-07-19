// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request header metadata used during routing.
/// </summary>
internal sealed class HeaderMetadata : IHeaderMetadata
{
    public HeaderMetadata(IReadOnlyList<HeaderMatcher> matchers)
    {
        ArgumentNullException.ThrowIfNull(matchers);
        Matchers = matchers.ToArray();
    }

    /// <inheritdoc/>
    public HeaderMatcher[] Matchers { get; }
}
