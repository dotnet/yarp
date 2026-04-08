// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Yarp.Application.Tests;

public class SpaFallbackTests
{
    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?>? config = null)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));

                if (config is not null)
                {
                    builder.ConfigureAppConfiguration((context, configBuilder) =>
                    {
                        configBuilder.AddInMemoryCollection(config);
                    });
                }
            });

        return factory;
    }

    private static Dictionary<string, string?> StaticFilesEnabled() => new()
    {
        ["YARP_ENABLE_STATIC_FILES"] = "true"
    };

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForUnknownRoutes()
    {
        using var factory = CreateFactory(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForDeepNestedRoutes()
    {
        using var factory = CreateFactory(StaticFilesEnabled());
        using var client = factory.CreateClient();

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
        using var factory = CreateFactory(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_ServesStaticFiles_Directly()
    {
        using var factory = CreateFactory(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("color: red", content);
    }

    [Fact]
    public async Task SpaFallback_DefaultDocument_ServesIndexHtml_AtRoot()
    {
        // UseFileServer enables default documents, so / resolves to /index.html
        using var factory = CreateFactory(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_Disabled_Returns404_ForUnknownRoutes()
    {
        using var factory = CreateFactory(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["YARP_DISABLE_SPA_FALLBACK"] = "true"
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_Disabled_StillServesStaticFiles()
    {
        using var factory = CreateFactory(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["YARP_DISABLE_SPA_FALLBACK"] = "true"
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("color: red", content);
    }

    [Fact]
    public async Task StaticFiles_Disabled_Returns404_ForAll()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

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
        using var factory = CreateFactory(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["ReverseProxy:Routes:api:ClusterId"] = "backend",
            ["ReverseProxy:Routes:api:Match:Path"] = "/api/{**catch-all}",
            ["ReverseProxy:Clusters:backend:Destinations:d1:Address"] = "https://localhost:9999"
        });
        using var client = factory.CreateClient();

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
        using var factory = CreateFactory(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["ReverseProxy:Routes:catchall:ClusterId"] = "backend",
            ["ReverseProxy:Routes:catchall:Match:Path"] = "{**catch-all}",
            ["ReverseProxy:Clusters:backend:Destinations:d1:Address"] = "https://localhost:9999"
        });
        using var client = factory.CreateClient();

        // YARP catch-all route wins — request goes to proxy (which will fail
        // since there's no real backend, but it won't return index.html)
        var response = await client.GetAsync("/some/spa/route");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
