// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Health;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding proxy middleware to the pipeline.
/// </summary>
public static class AppBuilderHealthExtensions
{
    /// <summary>
    /// Passively checks destinations health by watching for successes and failures in client request proxying.
    /// </summary>
    public static IReverseProxyApplicationBuilder UsePassiveHealthChecks(this IReverseProxyApplicationBuilder builder)
    {
        builder.UseMiddleware<PassiveHealthCheckMiddleware>();
        return builder;
    }
}
