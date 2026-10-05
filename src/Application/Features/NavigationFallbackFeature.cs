// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class NavigationFallbackFeature
{
    public static WebApplication MapNavigationFallback(this WebApplication app, YarpAppConfig config)
    {
        if (config.NavigationFallback.Path is not null)
        {
            var fallback = app.MapFallbackToFile(config.NavigationFallback.Path);
            // Mark the SPA fallback endpoint so the fallback-specific middleware can distinguish
            // it from reverse proxy endpoints, while static files continue to flow through
            // StaticFileMiddleware without endpoint metadata.
            fallback.Add(endpointBuilder => endpointBuilder.Metadata.Add(NavigationFallbackEndpointMetadata.Instance));
        }

        return app;
    }
}
