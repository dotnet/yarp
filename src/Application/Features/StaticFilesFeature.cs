// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class StaticFilesFeature
{
    public static WebApplication UseStaticFiles(this WebApplication app, YarpAppConfig config)
    {
        if (config.StaticFiles.Enabled)
        {
            // Same as UseFileServer(), plus a marker so request logging can tell a file was served.
            var options = new FileServerOptions();
            options.StaticFileOptions.OnPrepareResponse = context =>
                context.Context.Items[RequestLoggingFeature.HandlerKey] = RequestLoggingFeature.StaticHandler;
            app.UseFileServer(options);
        }

        return app;
    }
}
