// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.SessionAffinity;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding proxy middleware to the pipeline.
/// </summary>
public static class AppBuilderSessionAffinityExtensions
{
    /// <summary>
    /// Checks if a request has an established affinity relationship and if the associated destination is available.
    /// This should be placed before load balancing and other destination selection components.
    /// Requests without an affinity relationship will be processed normally and have the affinity relationship
    /// established by a later component.
    /// </summary>
    public static IReverseProxyApplicationBuilder UseSessionAffinity(this IReverseProxyApplicationBuilder builder)
    {
        builder.UseMiddleware<SessionAffinityMiddleware>();
        return builder;
    }
}
