// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Common;

public class TestEnvironment
{
    public ITestOutputHelper TestOutput { get; set; }

    public HttpProtocols ProxyProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public bool UseHttpsOnProxy { get; set; }

    public Encoding HeaderEncoding { get; set; }

    public Action<IServiceCollection> ConfigureProxyServices { get; set; } = _ => { };

    public Action<IReverseProxyBuilder> ConfigureProxy { get; set; } = _ => { };

    public Action<IApplicationBuilder> ConfigureProxyApp { get; set; } = _ => { };

    public string ClusterId { get; set; } = "cluster1";

    public Func<ClusterConfig, RouteConfig, (ClusterConfig Cluster, RouteConfig Route)> ConfigTransformer { get; set; } = (a, b) => (a, b);

    public Version DestinationHttpVersion { get; set; }

    public HttpVersionPolicy? DestinationHttpVersionPolicy { get; set; }

    public HttpProtocols DestinationProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public bool UseHttpsOnDestination { get; set; }

    public bool UseHttpSysOnDestination { get; set; }

    public Action<IServiceCollection> ConfigureDestinationServices { get; set; } = _ => { };

    public Action<IApplicationBuilder> ConfigureDestinationApp { get; set; } = _ => { };

    public TestEnvironment() { }

    public TestEnvironment(RequestDelegate destinationGetDelegate)
    {
        ConfigureDestinationApp = destinationApp =>
        {
            destinationApp.Run(destinationGetDelegate);
        };
    }

    public async Task Invoke(Func<string, Task> clientFunc, CancellationToken cancellationToken = default)
    {
        using var destination = CreateHost(DestinationProtocol, UseHttpsOnDestination, HeaderEncoding,
            ConfigureDestinationServices, ConfigureDestinationApp, UseHttpSysOnDestination);
        await destination.StartAsync(cancellationToken);

        Exception proxyException = null;
        using var proxy = CreateProxy(destination.GetAddress(), ex => proxyException = ex);
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

        Assert.Null(proxyException);
    }

    public IHost CreateProxy(string destinationAddress, Action<Exception> onProxyException = null)
    {
        return CreateHost(ProxyProtocol, UseHttpsOnProxy, HeaderEncoding,
            services =>
            {
                ConfigureProxyServices(services);

                var route = new RouteConfig
                {
                    RouteId = "route1",
                    ClusterId = ClusterId,
                    Match = new RouteMatch { Path = "/{**catchall}" }
                };

                var cluster = new ClusterConfig
                {
                    ClusterId = ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1",  new DestinationConfig() { Address = destinationAddress } }
                    },
                    HttpClient = new HttpClientConfig
                    {
                        DangerousAcceptAnyServerCertificate = UseHttpsOnDestination,
                        RequestHeaderEncoding = HeaderEncoding?.WebName,
                    },
                    HttpRequest = new Forwarder.ForwarderRequestConfig
                    {
                        Version = DestinationHttpVersion,
                        VersionPolicy = DestinationHttpVersionPolicy,
                    }
                };
                (cluster, route) = ConfigTransformer(cluster, route);
                var proxyBuilder = services.AddReverseProxy().LoadFromMemory(new[] { route }, new[] { cluster });
                ConfigureProxy(proxyBuilder);
            },
            app =>
            {
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        onProxyException?.Invoke(ex);
                        throw;
                    }
                });

                ConfigureProxyApp(app);
                app.UseRouting();
                app.UseEndpoints(builder =>
                {
                    builder.MapReverseProxy();
                });
            });
    }

    private IHost CreateHost(HttpProtocols protocols, bool useHttps, Encoding requestHeaderEncoding,
        Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp, bool useHttpSys = false)
    {
        return new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { "Logging:LogLevel:Yarp", "Trace" },
                    { "Logging:LogLevel:Microsoft", "Trace" },
                    { "Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics", "Information" }
                });
            })
            .ConfigureLogging((hostingContext, loggingBuilder) =>
            {
                loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                loggingBuilder.AddEventSourceLogger();
                if (TestOutput != null)
                {
                    loggingBuilder.Services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(TestOutput));
                }
            })
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .ConfigureServices(configureServices)
                    .UseKestrel(kestrel =>
                    {
                        if (requestHeaderEncoding is not null)
                        {
                            kestrel.RequestHeaderEncodingSelector = _ => requestHeaderEncoding;
                        }
                        kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
                        {
                            listenOptions.Protocols = protocols;
                            if (useHttps)
                            {
                                listenOptions.UseHttps(TestResources.GetTestCertificate());
                            }
                            listenOptions.UseConnectionLogging();
                        });
                    })
                    .Configure(configureApp);
                if (useHttpSys)
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    webHostBuilder.UseHttpSys(httpSys =>
                    {
                        if (useHttps)
                        {
                            httpSys.UrlPrefixes.Add("https://localhost:" + FindHttpSysHttpsPortAsync(TestOutput).Result);
                        }
                        else
                        {
                            httpSys.UrlPrefixes.Add("http://localhost:0");
                        }
                    });
#pragma warning restore CA1416 // Validate platform compatibility
                }
            }).Build();
    }

    private const int BaseHttpsPort = 44300;
    private const int MaxHttpsPort = 44399;
    private static int NextHttpsPort = BaseHttpsPort;
    private static readonly SemaphoreSlim PortLock = new SemaphoreSlim(1);

    internal static async Task<int> FindHttpSysHttpsPortAsync(ITestOutputHelper output)
    {
        await PortLock.WaitAsync();
        try
        {
            while (NextHttpsPort < MaxHttpsPort)
            {
                var port = NextHttpsPort++;
                using var host = new HostBuilder()
                    .ConfigureAppConfiguration(config =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string>()
                        {
                            { "Logging:LogLevel:Microsoft", "Trace" },
                        });
                    })
                    .ConfigureLogging((hostingContext, loggingBuilder) =>
                    {
                        loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                        loggingBuilder.AddEventSourceLogger();
                        loggingBuilder.AddXunit(output);
                    })
                    .ConfigureWebHost(webHostBuilder =>
                    {
#pragma warning disable CA1416 // Validate platform compatibility
                        webHostBuilder.UseHttpSys(httpSys =>
                        {
                            httpSys.UrlPrefixes.Add("https://localhost:" + port);
                        });
                        webHostBuilder.Configure(app => { });
#pragma warning restore CA1416 // Validate platform compatibility
                    }).Build();

                try
                {
                    await host.StartAsync();
                    await host.StopAsync();
                    return port;
                }
                catch (HttpSysException)
                {
                }
            }
            NextHttpsPort = BaseHttpsPort;
        }
        finally
        {
            PortLock.Release();
        }
        throw new Exception("Failed to locate a free port.");
    }
}
