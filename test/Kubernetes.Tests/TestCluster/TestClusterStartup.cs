// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestClusterStartup
{
#pragma warning disable CA1822 // Mark members as static
    public void ConfigureServices(IServiceCollection services)
#pragma warning restore CA1822 // Mark members as static
    {
        // services.AddTransient<ResourceManager>();
        services.AddControllers();
        services.AddSingleton<ITestCluster, TestCluster>();
    }

#pragma warning disable CA1822 // Mark members as static
    public void Configure(IApplicationBuilder app, ITestCluster cluster)
#pragma warning restore CA1822 // Mark members as static
    {
        if (app is null)
        {
            throw new System.ArgumentNullException(nameof(app));
        }

        if (cluster is null)
        {
            throw new System.ArgumentNullException(nameof(cluster));
        }

        app.Use(next => async context =>
        {
            // This is a no-op, but very convenient for setting a breakpoint to see per-request details.
            await next(context);
        });
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
        app.Run(cluster.UnhandledRequest);
    }
}
