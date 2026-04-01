// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.Application.Tests;

public class SpaFallbackTests
{
    private static WebApplication CreateApp(string webRoot, Dictionary<string, string?>? config = null)
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
                        .LoadFromMemory([], []);

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

    private static string GetWebRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }

    [Fact]
    public async Task SpaFallback_ReturnsIndexHtml_ForUnknownRoutes()
    {
        await using var app = CreateApp(GetWebRoot(), new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true"
        });
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SPA Index", content);
    }

    [Fact]
    public async Task SpaFallback_ServesStaticFiles_Directly()
    {
        await using var app = CreateApp(GetWebRoot(), new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true"
        });
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("color: red", content);
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
    public async Task StaticFiles_Disabled_Returns404_ForAll()
    {
        await using var app = CreateApp(GetWebRoot());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var indexResponse = await client.GetAsync("/index.html");
        var spaResponse = await client.GetAsync("/some/route");

        Assert.Equal(HttpStatusCode.NotFound, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, spaResponse.StatusCode);
    }
}
