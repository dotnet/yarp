// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Common;

public class HttpSysTestEnvironment
{
    private readonly Action<IServiceCollection> _configureDestinationServices;
    private readonly Action<HttpSysOptions> _configureDestinationHttpSysOptions;
    private readonly Action<IApplicationBuilder> _configureDestinationApp;
    private readonly Action<IServiceCollection> _configureProxyServices;
    private readonly Action<IReverseProxyBuilder> _configureProxy;
    private readonly Action<IApplicationBuilder> _configureProxyApp;
    private readonly Action<IReverseProxyApplicationBuilder> _configureProxyPipeline;
    private readonly Func<ClusterConfig, RouteConfig, (ClusterConfig Cluster, RouteConfig Route)> _configTransformer;

    public string ClusterId { get; set; } = "cluster1";

    public HttpSysTestEnvironment(
        Action<IServiceCollection> configureDestinationServices,
        Action<HttpSysOptions> configureDestinationHttpSysOptions,
        Action<IApplicationBuilder> configureDestinationApp,
        Action<IServiceCollection> configureProxyServices,
        Action<IReverseProxyBuilder> configureProxy,
        Action<IApplicationBuilder> configureProxyApp,
        Action<IReverseProxyApplicationBuilder> configureProxyPipeline,
        Func<ClusterConfig, RouteConfig, (ClusterConfig Cluster, RouteConfig Route)> configTransformer = null)
    {
        _configureDestinationServices = configureDestinationServices;
        _configureDestinationHttpSysOptions = configureDestinationHttpSysOptions;
        _configureDestinationApp = configureDestinationApp;
        _configureProxy = configureProxy;
        _configureProxyApp = configureProxyApp;
        _configureProxyPipeline = configureProxyPipeline;
        _configureProxyServices = configureProxyServices ?? (_ => { });
        _configTransformer = configTransformer ?? ((ClusterConfig c, RouteConfig r) => (c, r));
    }

    public async Task Invoke(Func<string, Task> clientFunc, CancellationToken cancellationToken = default)
    {
        using var destination = CreateHttpSysHost(_configureDestinationServices, _configureDestinationHttpSysOptions, _configureDestinationApp);
        await destination.StartAsync(cancellationToken);

        using var proxy = CreateHttpSysProxy(
            ClusterId,
            destination.GetAddress(),
            _configureProxyServices,
            _configureProxy,
            _configureProxyApp,
            _configureProxyPipeline,
            _configTransformer);
        await proxy.StartAsync(cancellationToken);

        try
        {
            await clientFunc(proxy.GetAddress());
        }
        finally
        {
            await proxy.StopAsync(cancellationToken);
            await destination.StopAsync(cancellationToken);
        }
    }

    private static IHost CreateHttpSysProxy(
        string clusterId,
        string destinationAddress,
        Action<IServiceCollection> configureServices,
        Action<IReverseProxyBuilder> configureProxy,
        Action<IApplicationBuilder> configureProxyApp,
        Action<IReverseProxyApplicationBuilder> configureProxyPipeline,
        Func<ClusterConfig, RouteConfig, (ClusterConfig Cluster, RouteConfig Route)> configTransformer)
    {
        return CreateHttpSysHost(
            services =>
            {
                configureServices(services);

                var route = new RouteConfig
                {
                    RouteId = "route1",
                    ClusterId = clusterId,
                    Match = new RouteMatch { Path = "/{**catchall}" }
                };

                var cluster = new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1",  new DestinationConfig() { Address = destinationAddress } }
                    },
                };
                (cluster, route) = configTransformer(cluster, route);
                var proxyBuilder = services.AddReverseProxy().LoadFromMemory(new[] { route }, new[] { cluster });
                configureProxy(proxyBuilder);
            },
            httpSysOptions => { },
            app =>
            {
                configureProxyApp(app);
                app.UseRouting();
                app.UseEndpoints(builder =>
                {
                    if (configureProxyPipeline != null)
                    {
                        builder.MapReverseProxy(configureProxyPipeline);
                    }
                    else
                    {
                        builder.MapReverseProxy();
                    }
                });
            });
    }

    private static IHost CreateHttpSysHost(
        Action<IServiceCollection> configureServices,
        Action<HttpSysOptions> configureHttpSys,
        Action<IApplicationBuilder> configureApp)
    {
        return CreateHost(webHostBuilder =>
        {
            Debug.Assert(OperatingSystem.IsWindows());
            webHostBuilder
               .ConfigureServices(configureServices)
               .UseHttpSys(options =>
               {
                   options.UrlPrefixes.Add(UrlPrefix.Create("http", "localhost", "0", "/"));
                   configureHttpSys(options);
               })
               .Configure(configureApp);
        });
    }

    private static IHost CreateHost(Action<IWebHostBuilder> configureWebHost)
    {
        return new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { "Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics", "Information" }
                });
            })
            .ConfigureLogging((hostingContext, loggingBuilder) =>
            {
                loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                loggingBuilder.AddEventSourceLogger();
            })
            .ConfigureWebHost(configureWebHost)
            .Build();
    }
}
