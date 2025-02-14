// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.WebSocketsTelemetry;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// <see cref="IApplicationBuilder"/> extension methods to add the <see cref="WebSocketsTelemetryMiddleware"/>.
/// </summary>
public static class WebSocketsTelemetryExtensions
{
    /// <summary>
    /// Adds a <see cref="WebSocketsTelemetryMiddleware"/> to the request pipeline.
    /// Must be added before <see cref="WebSockets.WebSocketMiddleware"/>.
    /// </summary>
    public static IApplicationBuilder UseWebSocketsTelemetry(this IApplicationBuilder app)
    {
        return app.Use(next =>
        {
            // UseWebSocketsTelemetry may be used independently of the rest of YARP.
            // Avoid exposing another extension method (AddWebSocketsTelemetry) just because of TimeProvider.
            var timeProvider = app.ApplicationServices.GetService<TimeProvider>() ?? TimeProvider.System;
            return new WebSocketsTelemetryMiddleware(next, timeProvider).InvokeAsync;
        });
    }
}
