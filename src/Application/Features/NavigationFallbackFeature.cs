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
            // The fallback handler clears the matched endpoint before serving the file,
            // so record the handler on the HttpContext rather than reading endpoint metadata later.
            app.MapFallbackToFile(config.NavigationFallback.Path)
                .Add(builder =>
                {
                    var inner = builder.RequestDelegate!;
                    builder.RequestDelegate = context =>
                    {
                        context.Items[RequestLoggingFeature.HandlerKey] = RequestLoggingFeature.FallbackHandler;
                        return inner(context);
                    };
                });
        }

        return app;
    }
}
