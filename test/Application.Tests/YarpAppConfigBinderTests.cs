// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Xunit;
using Yarp.Application.Configuration;

namespace Yarp.Application.Tests;

public class YarpAppConfigBinderTests
{
    private static YarpAppConfig Bind(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        return YarpAppConfigBinder.Bind(configuration);
    }

    // New config format

    [Fact]
    public void Bind_StaticFilesEnabled()
    {
        var config = Bind(new() { ["StaticFiles:Enabled"] = "true" });
        Assert.True(config.StaticFiles.Enabled);
    }

    [Fact]
    public void Bind_NavigationFallbackPath()
    {
        var config = Bind(new() { ["NavigationFallback:Path"] = "/app.html" });
        Assert.Equal("/app.html", config.NavigationFallback.Path);
    }

    [Fact]
    public void Bind_NavigationFallbackExclude()
    {
        var config = Bind(new()
        {
            ["NavigationFallback:Exclude:0:Path"] = "/api/{**catch-all}",
            ["NavigationFallback:Exclude:1:Path"] = "/.well-known/{**catch-all}"
        });
        Assert.Collection(
            config.NavigationFallback.Exclude,
            match => Assert.Equal("/api/{**catch-all}", match.Path),
            match => Assert.Equal("/.well-known/{**catch-all}", match.Path));
    }

    [Fact]
    public void Bind_HeaderRules()
    {
        var config = Bind(new()
        {
            ["Headers:0:Match:Path"] = "/{**path}",
            ["Headers:0:Set:X-Test"] = "applied",
            ["Headers:1:Match:Path"] = "/_astro/{**path}",
            ["Headers:1:Set:Cache-Control"] = "public, max-age=31536000, immutable"
        });

        Assert.Collection(
            config.Headers,
            rule =>
            {
                Assert.Equal("/{**path}", rule.Match.Path);
                Assert.Equal("applied", rule.Set["X-Test"]);
            },
            rule =>
            {
                Assert.Equal("/_astro/{**path}", rule.Match.Path);
                Assert.Equal("public, max-age=31536000, immutable", rule.Set["Cache-Control"]);
            });
    }

    [Fact]
    public void Bind_RedirectRules()
    {
        var config = Bind(new()
        {
            ["Redirects:0:Match:Path"] = "/old-page",
            ["Redirects:0:Destination"] = "/new-page",
            ["Redirects:0:StatusCode"] = "302"
        });

        var rule = Assert.Single(config.Redirects);
        Assert.Equal("/old-page", rule.Match.Path);
        Assert.Equal("/new-page", rule.Destination);
        Assert.Equal(302, rule.StatusCode);
    }

    [Fact]
    public void Bind_TelemetryUnsafeCert()
    {
        var config = Bind(new() { ["Telemetry:UnsafeAcceptAnyCertificate"] = "true" });
        Assert.True(config.Telemetry.UnsafeAcceptAnyCertificate);
    }

    [Fact]
    public void Bind_Defaults_EverythingOff()
    {
        var config = Bind(new());
        Assert.False(config.StaticFiles.Enabled);
        Assert.Null(config.NavigationFallback.Path);
        Assert.Empty(config.NavigationFallback.Exclude);
        Assert.Empty(config.Headers);
        Assert.Empty(config.Redirects);
        Assert.False(config.Telemetry.UnsafeAcceptAnyCertificate);
    }

    // Legacy key mapping

    [Fact]
    public void Legacy_EnableStaticFiles()
    {
        var config = Bind(new() { ["YARP_ENABLE_STATIC_FILES"] = "true" });
        Assert.True(config.StaticFiles.Enabled);
    }

    [Fact]
    public void Legacy_EnableStaticFiles_ImpliesFallback()
    {
        var config = Bind(new() { ["YARP_ENABLE_STATIC_FILES"] = "true" });
        Assert.Equal("/index.html", config.NavigationFallback.Path);
    }

    [Fact]
    public void Legacy_EnableStaticFiles_ImpliesFallback_WhenOnlyFallbackExclusionsConfigured()
    {
        var config = Bind(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["NavigationFallback:Exclude:0:Path"] = "/api/{**catch-all}"
        });

        Assert.Equal("/index.html", config.NavigationFallback.Path);
        var exclusion = Assert.Single(config.NavigationFallback.Exclude);
        Assert.Equal("/api/{**catch-all}", exclusion.Path);
    }

    [Fact]
    public void Legacy_DisableSpaFallback()
    {
        var config = Bind(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["YARP_DISABLE_SPA_FALLBACK"] = "true"
        });
        Assert.True(config.StaticFiles.Enabled);
        Assert.Null(config.NavigationFallback.Path);
    }

    [Fact]
    public void Legacy_UnsafeCert()
    {
        var config = Bind(new() { ["YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE"] = "true" });
        Assert.True(config.Telemetry.UnsafeAcceptAnyCertificate);
    }

    // Precedence: new config wins over legacy

    [Fact]
    public void Precedence_ExplicitFallbackPathWinsOverLegacyFallback()
    {
        var config = Bind(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["NavigationFallback:Path"] = "/custom.html"
        });

        Assert.Equal("/custom.html", config.NavigationFallback.Path);
    }

    [Fact]
    public void Precedence_ExplicitFallbackWinsOverLegacy()
    {
        var config = Bind(new()
        {
            ["YARP_ENABLE_STATIC_FILES"] = "true",
            ["NavigationFallback:Path"] = "/custom.html"
        });
        // Explicit NavigationFallback takes precedence over legacy default
        Assert.Equal("/custom.html", config.NavigationFallback.Path);
    }

    [Fact]
    public void Precedence_NewTelemetryWinsOverLegacy()
    {
        var config = Bind(new()
        {
            ["Telemetry:UnsafeAcceptAnyCertificate"] = "true",
            ["YARP_UNSAFE_OLTP_CERT_ACCEPT_ANY_SERVER_CERTIFICATE"] = "false"
        });
        Assert.True(config.Telemetry.UnsafeAcceptAnyCertificate);
    }

    [Fact]
    public void Legacy_StaticFilesDisabled_NoFallback()
    {
        // Without YARP_ENABLE_STATIC_FILES, no fallback should be set
        var config = Bind(new());
        Assert.Null(config.NavigationFallback.Path);
    }

    [Fact]
    public void Legacy_CaseInsensitive()
    {
        var config = Bind(new() { ["YARP_ENABLE_STATIC_FILES"] = "True" });
        Assert.True(config.StaticFiles.Enabled);
    }
}
