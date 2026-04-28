// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class NavigationFallbackExclusionsFeature
{
    public static WebApplication MapNavigationFallbackExclusions(this WebApplication app, YarpAppConfig config)
    {
        if (config.NavigationFallback.Path is null || config.NavigationFallback.Exclude.Count == 0)
        {
            return app;
        }

        for (var i = 0; i < config.NavigationFallback.Exclude.Count; i++)
        {
            var path = RequestMatchEvaluator.ValidatePath(config.NavigationFallback.Exclude[i], "NavigationFallback exclusion");
            var order = int.MaxValue - 1000 + i;
            app.MapFallback(
                    path,
                    context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return Task.CompletedTask;
                    })
                .Add(endpointBuilder =>
                {
                    endpointBuilder.DisplayName = $"Fallback exclusion {path}";
                    // Keep exclusions just ahead of the SPA fallback while still letting proxy
                    // routes and other normal endpoints win when they match.
                    ((RouteEndpointBuilder)endpointBuilder).Order = order;
                });
        }

        return app;
    }
}
