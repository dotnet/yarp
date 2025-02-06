// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Delegation;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extensions for adding delegation middleware to the pipeline.
/// </summary>
public static class AppBuilderDelegationExtensions
{
    /// <summary>
    /// Adds middleware to check if the selected destination should use Http.sys delegation.
    /// If so, the request is delegated to the destination queue instead of being proxied over HTTP.
    /// This should be placed after load balancing and passive health checks.
    /// </summary>
    /// <remarks>
    /// This middleware only works with the ASP.NET Core Http.sys server implementation.
    /// </remarks>
    public static IReverseProxyApplicationBuilder UseHttpSysDelegation(this IReverseProxyApplicationBuilder builder)
    {
        // IServerDelegationFeature isn't added to DI https://github.com/dotnet/aspnetcore/issues/40043
        _ = builder.ApplicationServices.GetRequiredService<IServer>().Features?.Get<IServerDelegationFeature>()
            ?? throw new NotSupportedException($"{typeof(IHttpSysRequestDelegationFeature).FullName} is not available. Http.sys delegation is only supported when using the Http.sys server");

        builder.UseMiddleware<HttpSysDelegatorMiddleware>();
        return builder;
    }
}
