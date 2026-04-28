// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class StaticFilesFeature
{
    private static readonly object PreservedEndpointKey = new();

    public static WebApplication UseStaticFiles(this WebApplication app, YarpAppConfig config)
    {
        if (!config.StaticFiles.Enabled)
        {
            return app;
        }

        app.Use((context, next) =>
        {
            var endpoint = context.GetEndpoint();
            if (endpoint?.RequestDelegate is null)
            {
                return next();
            }

            // Redirects short-circuit during UseRouting, but normal routed endpoints would block
            // StaticFileMiddleware once UseRouting runs first. Clear the selected endpoint around
            // UseFileServer so static files still win when a physical file exists, then restore it
            // for later middleware and endpoint execution if no file handled the request.
            context.Items[PreservedEndpointKey] = endpoint;
            context.SetEndpoint(null);
            return next();
        });

        var onPrepareResponse = StaticHostHeadersFeature.CreateStaticFileHeaderCallback(config);
        if (onPrepareResponse is null)
        {
            app.UseFileServer();
        }
        else
        {
            // UseFileServer keeps default documents + static file serving together; only the
            // response preparation callback changes when header rules are configured.
            app.UseFileServer(new FileServerOptions
            {
                StaticFileOptions =
                {
                    OnPrepareResponse = onPrepareResponse
                }
            });
        }

        app.Use((context, next) =>
        {
            if (!context.Response.HasStarted
                && context.GetEndpoint() is null
                && context.Items.TryGetValue(PreservedEndpointKey, out var endpoint)
                && endpoint is Endpoint preservedEndpoint)
            {
                context.SetEndpoint(preservedEndpoint);
            }

            return next();
        });

        return app;
    }
}
