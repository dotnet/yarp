// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Application.Tests;

public class SpaFallbackTests
{
    private static WebApplication CreateApp(string webRoot, Dictionary<string, string?>? config = null,
        IReadOnlyList<RouteConfig>? routes = null, IReadOnlyList<ClusterConfig>? clusters = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = webRoot
        });

        if (config is not null)
        {
            builder.Configuration.AddInMemoryCollection(config);
        }

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddReverseProxy()
                        .LoadFromMemory(routes ?? [], clusters ?? []);

        var app = builder.Build();

        var enableStaticFiles = string.Equals(app.Configuration["YARP_ENABLE_STATIC_FILES"], "true", StringComparison.OrdinalIgnoreCase);
        if (enableStaticFiles)
        {
            app.UseFileServer();
        }

        app.UseRouting();
        app.MapReverseProxy();

        if (enableStaticFiles)
        {
            var disableSpaFallback = string.Equals(app.Configuration["YARP_DISABLE_SPA_FALLBACK"], "true", StringComparison.OrdinalIgnoreCase);
            if (!disableSpaFallback)
            {
                app.MapFallbackToFile("index.html");
            }
        }

        return app;
    }

    private static Dictionary<string, string?> StaticFilesEnabled() => new()
    {
        ["YARP_ENABLE_STATIC_FILES"] = "true"
    };

    private static string GetWebRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForUnknownRoutes()
    {
        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForDeepNestedRoutes()
    {
        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/app/settings/profile/edit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_Returns404_ForMissingFileExtensions()
    {
        // MapFallbackToFile uses {*path:nonfile} so requests with file extensions
        // are not caught by the fallback — this prevents broken asset requests
        // from returning index.html
        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_ServesStaticFiles_Directly()
    {
        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("color: red", content);
    }

    [Fact]
    public async Task SpaFallback_DefaultDocument_ServesIndexHtml_AtRoot()
    {
        // UseFileServer enables default documents, so / resolves to /index.html
        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_MissingIndexHtml_Returns404()
    {
        // Use an empty webroot with no index.html
        var emptyWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot-empty");
        Directory.CreateDirectory(emptyWebRoot);

        try
        {
            await using var app = CreateApp(emptyWebRoot, StaticFilesEnabled());
            await app.StartAsync();

            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

            var response = await client.GetAsync("/some/spa/route");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            Directory.Delete(emptyWebRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SpaFallback_Disabled_Returns404_ForUnknownRoutes()
    {
        await using var app = CreateApp(GetWebRoot(), new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["YARP_DISABLE_SPA_FALLBACK"] = "true"
        });
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_Disabled_StillServesStaticFiles()
    {
        await using var app = CreateApp(GetWebRoot(), new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["YARP_DISABLE_SPA_FALLBACK"] = "true"
        });
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("color: red", content);
    }

    [Fact]
    public async Task StaticFiles_Disabled_Returns404_ForAll()
    {
        await using var app = CreateApp(GetWebRoot());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var indexResponse = await client.GetAsync("/index.html");
        var spaResponse = await client.GetAsync("/some/route");
        var cssResponse = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.NotFound, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, spaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cssResponse.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_CoexistsWithSpecificYarpRoutes()
    {
        // When YARP has specific routes (e.g. /api/), non-YARP paths
        // should still fall back to index.html for SPA routing
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = "backend",
                Match = new RouteMatch { Path = "/api/{**catch-all}" }
            }
        };
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "backend",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["d1"] = new DestinationConfig { Address = "https://localhost:9999" }
                }
            }
        };

        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled(), routes, clusters);
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // SPA route should still get index.html
        var spaResponse = await client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, spaResponse.StatusCode);
        var content = await spaResponse.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);

        // Static files should still be served
        var cssResponse = await client.GetAsync("/style.css");
        Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_CatchAllYarpRoute_WinsOverFallback()
    {
        // When YARP has a catch-all route, it takes priority over the
        // SPA fallback — this is expected because YARP owns all routing
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "catchall",
                ClusterId = "backend",
                Match = new RouteMatch { Path = "{**catch-all}" }
            }
        };
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "backend",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["d1"] = new DestinationConfig { Address = "https://localhost:9999" }
                }
            }
        };

        await using var app = CreateApp(GetWebRoot(), StaticFilesEnabled(), routes, clusters);
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        // YARP catch-all route wins — request goes to proxy (which will fail
        // since there's no real backend, but it won't return index.html)
        var response = await client.GetAsync("/some/spa/route");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
