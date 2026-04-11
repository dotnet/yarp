// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Yarp.Application.Features;

public static class ReverseProxyFeature
{
    public static IHostApplicationBuilder AddReverseProxy(this IHostApplicationBuilder builder, IConfiguration configuration)
    {
        builder.Services.AddServiceDiscovery();
        builder.Services.AddReverseProxy()
                        .LoadFromConfig(configuration.GetSection("ReverseProxy"))
                        .AddServiceDiscoveryDestinationResolver();

        return builder;
    }
}
