// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing.Tests;

public class RoutingTests
{
    [Fact]
    public async Task PathRouting_Works()
    {
        var routes = new[]
        {
            new RouteConfig()
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch { Path = "/api/{**catchall}" }
            }
        };

        using var host = await CreateHostAsync(routes);
        var client = host.GetTestClient();

        // Positive
        var response = await client.GetAsync("/api/extra");
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        // Negative
        response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HostRouting_Works()
    {
        var routes = new[]
        {
            new RouteConfig()
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch { Hosts = new[] { "*.example.com" } }
            }
        };

        using var host = await CreateHostAsync(routes);
        var client = host.GetTestClient();

        // Positive
        var response = await client.GetAsync("http://foo.example.com/");
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        // Negative
        response = await client.GetAsync("http://example.com");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HeaderRouting_OneHeader_Works()
    {
        var routes = new[]
        {
            new RouteConfig()
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                        }
                    }
                }
            }
        };

        using var host = await CreateHostAsync(routes);
        var client = host.GetTestClient();

        // Positive
        var request = new HttpRequestMessage();
        request.Headers.Add("header1", "value1");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        // Negative
        response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        request = new HttpRequestMessage();
        request.Headers.Add("header2", "value1");
        response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        request = new HttpRequestMessage();
        request.Headers.Add("header1", "v");
        response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        request = new HttpRequestMessage();
        request.Headers.Add("header1", (string)null);
        response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HeaderRouting_MultipleHeaders_Works()
    {
        var routes = new[]
        {
            new RouteConfig()
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                        }
                    }
                }
            },
            new RouteConfig()
            {
                RouteId = "route2",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header2",
                            Values = new[] { "value2" },
                        }
                    }
                }
            },
            new RouteConfig()
            {
                RouteId = "route3",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                        },
                        new RouteHeader()
                        {
                            Name = "header2",
                            Values = new[] { "value2" },
                        }
                    }
                }
            }
        };

        using var host = await CreateHostAsync(routes);
        var client = host.GetTestClient();

        // Check for the most specific match
        var request = new HttpRequestMessage();
        request.Headers.Add("header1", "value1");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        request = new HttpRequestMessage();
        request.Headers.Add("header1", "value1");
        request.Headers.Add("header2", "value3");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        request = new HttpRequestMessage();
        request.Headers.Add("header2", "value2");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route2", response.Headers.GetValues("route").SingleOrDefault());

        request = new HttpRequestMessage();
        request.Headers.Add("header1", "value3");
        request.Headers.Add("header2", "value2");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route2", response.Headers.GetValues("route").SingleOrDefault());

        request = new HttpRequestMessage();
        request.Headers.Add("header1", "value1");
        request.Headers.Add("header2", "value2");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route3", response.Headers.GetValues("route").SingleOrDefault());

        // Negative
        response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        request = new HttpRequestMessage();
        request.Headers.Add("header1", "value2");
        request.Headers.Add("header2", "value1");
        response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Precedence_PathMethodHostHeaders()
    {
        var routes = new[]
        {
            new RouteConfig()
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch { Path = "/route1" }
            },
            new RouteConfig()
            {
                RouteId = "route2",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Methods = new[] { "GET" },
                }
            },
            new RouteConfig()
            {
                RouteId = "route3",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Hosts = new[] { "localhost" }
                }
            },
            new RouteConfig()
            {
                RouteId = "route4",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catchall}",
                    Headers = new[]
                    {
                        new RouteHeader()
                        {
                            Name = "header1",
                            Values = new[] { "value1" },
                        },
                    }
                }
            }
        };

        using var host = await CreateHostAsync(routes);
        var client = host.GetTestClient();

        // Check for the highest priority match

        // Path
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/route1");
        request.Headers.Add("header1", "value1");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route1", response.Headers.GetValues("route").SingleOrDefault());

        // Method
        request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        request.Headers.Add("header1", "value1");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route2", response.Headers.GetValues("route").SingleOrDefault());

        // Host
        request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/");
        request.Headers.Add("header1", "value1");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route3", response.Headers.GetValues("route").SingleOrDefault());

        // Header
        request = new HttpRequestMessage(HttpMethod.Post, "http://example/");
        request.Headers.Add("header1", "value1");
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Equal("route4", response.Headers.GetValues("route").SingleOrDefault());
    }

    public static Task<IHost> CreateHostAsync(IReadOnlyList<RouteConfig> routes)
    {
        var clusters = new[]
        {
            new ClusterConfig()
            {
                ClusterId = "cluster1",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { "d1", new DestinationConfig() { Address = "http://localhost/" }  }
                }
            }
        };

        return new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddReverseProxy()
                        .LoadFromMemory(routes, clusters);
                });
                webHost.Configure(appBuilder =>
                {
                    appBuilder.UseRouting();
                    appBuilder.UseEndpoints(endpoints =>
                    {
                        endpoints.MapReverseProxy(proxyApp =>
                        {
                            proxyApp.Run(context =>
                            {
                                var endpoint = context.GetEndpoint();
                                context.Response.Headers["route"] = endpoint.DisplayName;
                                return Task.CompletedTask;
                            });
                        });
                    });
                });
            }).StartAsync();
    }
}
