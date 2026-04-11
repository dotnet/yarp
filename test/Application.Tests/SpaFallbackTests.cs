// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using Yarp.Application.Configuration;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Yarp.Application.Tests;

internal class YarpTestApp : WebApplicationFactory<Program>
{
    private Action<IConfigurationBuilder>? _configAction;

    public void ConfigureConfiguration(Action<IConfigurationBuilder> configure)
    {
        _configAction += configure;
    }

    public void Configure(YarpAppConfig config)
    {
        ConfigureConfiguration(b =>
        {
            var json = JsonSerializer.Serialize(config);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            b.AddJsonStream(stream);
        });
    }

    public void Configure(Dictionary<string, string?> config)
    {
        ConfigureConfiguration(b => b.AddInMemoryCollection(config));
    }

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
        if (_configAction is { } a)
        {
            TestConfiguration.Create(a);
        }

        return base.CreateWebHostBuilder();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
    }
}

public class SpaFallbackTests
{
    private static YarpTestApp CreateApp(YarpAppConfig config)
    {
        var app = new YarpTestApp();
        app.Configure(config);
        return app;
    }

    private static YarpTestApp CreateApp(Dictionary<string, string?>? config = null)
    {
        var app = new YarpTestApp();
        if (config is not null)
        {
            app.Configure(config);
        }
        return app;
    }

    private static Dictionary<string, string?> StaticFilesEnabled() => new()
    {
        ["YARP_ENABLE_STATIC_FILES"] = "true"
    };

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForUnknownRoutes()
    {
        using var factory = CreateApp(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForDeepNestedRoutes()
    {
        using var factory = CreateApp(StaticFilesEnabled());
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
        using var factory = CreateApp(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SpaFallback_ServesStaticFiles_Directly()
    {
        using var factory = CreateApp(StaticFilesEnabled());
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
        using var factory = CreateApp(StaticFilesEnabled());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_Disabled_Returns404_ForUnknownRoutes()
    {
        using var factory = CreateApp(new Dictionary<string, string?>()
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
        using var factory = CreateApp(new Dictionary<string, string?>()
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
        using var factory = CreateApp();
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
        using var factory = CreateApp(new Dictionary<string, string?>()
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
        using var factory = CreateApp(new Dictionary<string, string?>()
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

    // Tests using the strongly-typed config object model

    [Fact]
    public async Task ObjectModel_StaticFilesAndFallback()
    {
        using var app = CreateApp(new YarpAppConfig
        {
            StaticFiles = { Enabled = true },
            NavigationFallback = { Path = "/index.html" }
        });
        using var client = app.CreateClient();

        var cssResponse = await client.GetAsync("/style.css");
        Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);

        var spaResponse = await client.GetAsync("/some/spa/route");
        Assert.Equal(HttpStatusCode.OK, spaResponse.StatusCode);
        var content = await spaResponse.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task ObjectModel_StaticFilesWithoutFallback()
    {
        using var app = CreateApp(new YarpAppConfig
        {
            StaticFiles = { Enabled = true }
        });
        using var client = app.CreateClient();

        var cssResponse = await client.GetAsync("/style.css");
        Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);

        var spaResponse = await client.GetAsync("/some/spa/route");
        Assert.Equal(HttpStatusCode.NotFound, spaResponse.StatusCode);
    }

    [Fact]
    public async Task ObjectModel_CustomFallbackPath()
    {
        using var app = CreateApp(new YarpAppConfig
        {
            StaticFiles = { Enabled = true },
            NavigationFallback = { Path = "/index.html" }
        });
        using var client = app.CreateClient();

        var response = await client.GetAsync("/deep/nested/route");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task ObjectModel_EverythingDisabled()
    {
        using var app = CreateApp(new YarpAppConfig());
        using var client = app.CreateClient();

        var indexResponse = await client.GetAsync("/index.html");
        var cssResponse = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.NotFound, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cssResponse.StatusCode);
    }
}
