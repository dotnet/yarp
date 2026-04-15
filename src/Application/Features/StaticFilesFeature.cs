// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Yarp.Application.Configuration;

namespace Yarp.Application.Features;

public static class StaticFilesFeature
{
    public static WebApplication UseStaticFiles(this WebApplication app, YarpAppConfig config)
    {
        if (config.StaticFiles.Enabled)
        {
            app.UseFileServer();
        }

        return app;
    }
}
