// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.Application.Tests;

public class SpaFallbackTests : IDisposable
{
    private readonly List<string> _envVarsToClean = new();

    private void SetEnv(string key, string? value)
    {
        _envVarsToClean.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    public void Dispose()
    {
        foreach (var key in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private WebApplication CreateApp(string webRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = webRoot
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddReverseProxy()
                        .LoadFromMemory([], []);

        var app = builder.Build();

        var isEnabledStaticFiles = Environment.GetEnvironmentVariable("YARP_ENABLE_STATIC_FILES");
        if (string.Equals(isEnabledStaticFiles, "true", StringComparison.OrdinalIgnoreCase))
        {
            app.UseFileServer();
        }

        app.UseRouting();
        app.MapReverseProxy();

        if (string.Equals(isEnabledStaticFiles, "true", StringComparison.OrdinalIgnoreCase))
        {
            var disableSpaFallback = Environment.GetEnvironmentVariable("YARP_DISABLE_SPA_FALLBACK");
            if (!string.Equals(disableSpaFallback, "true", StringComparison.OrdinalIgnoreCase))
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
        SetEnv("YARP_ENABLE_STATIC_FILES", "true");

        await using var app = CreateApp(GetWebRoot());
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
        SetEnv("YARP_ENABLE_STATIC_FILES", "true");

        await using var app = CreateApp(GetWebRoot());
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
        SetEnv("YARP_ENABLE_STATIC_FILES", "true");
        SetEnv("YARP_DISABLE_SPA_FALLBACK", "true");

        await using var app = CreateApp(GetWebRoot());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync("/some/spa/route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StaticFiles_Disabled_Returns404_ForAll()
    {
        // Don't set YARP_ENABLE_STATIC_FILES

        await using var app = CreateApp(GetWebRoot());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var indexResponse = await client.GetAsync("/index.html");
        var spaResponse = await client.GetAsync("/some/route");

        Assert.Equal(HttpStatusCode.NotFound, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, spaResponse.StatusCode);
    }
}
