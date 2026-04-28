// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Features;

// Marker metadata applied to the built-in SPA fallback endpoint.
// Static files do not become endpoints, so only fallback-specific middleware relies on this.
internal sealed class NavigationFallbackEndpointMetadata
{
    public static NavigationFallbackEndpointMetadata Instance { get; } = new();

    private NavigationFallbackEndpointMetadata()
    {
    }
}
