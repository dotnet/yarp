// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class RouteConfigTests
{
    [Fact]
    public void Equals_Positive()
    {
        var a = new RouteConfig()
        {
            AuthorizationPolicy = "a",
            RateLimiterPolicy = "rl",
            TimeoutPolicy = "t",
            Timeout = TimeSpan.FromSeconds(1),
            ClusterId = "c",
            CorsPolicy = "co",
            Match = new RouteMatch()
            {
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Hi",
                        Values = new[] { "v1", "v2" },
                        IsCaseSensitive = true,
                        Mode = HeaderMatchMode.HeaderPrefix,
                    }
                },
                Hosts = new[] { "foo:90" },
                Methods = new[] { "GET", "POST" },
                Path = "/p",
            },
            Metadata = new Dictionary<string, string>()
            {
                { "m", "m1" }
            },
            Order = 1,
            RouteId = "R",
        };
        var b = new RouteConfig()
        {
            AuthorizationPolicy = "A",
            RateLimiterPolicy = "RL",
            TimeoutPolicy = "T",
            Timeout = TimeSpan.FromSeconds(1),
            ClusterId = "C",
            CorsPolicy = "Co",
            Match = new RouteMatch()
            {
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "hi",
                        Values = new[] { "v1", "v2" },
                        IsCaseSensitive = true,
                        Mode = HeaderMatchMode.HeaderPrefix,
                    }
                },
                Hosts = new[] { "foo:90" },
                Methods = new[] { "GET", "POST" },
                Path = "/P"
            },
            Metadata = new Dictionary<string, string>()
            {
                { "m", "m1" }
            },
            Order = 1,
            RouteId = "r",
        };
        var c = b with { }; // Clone

        Assert.True(a.Equals(b));
        Assert.True(a.Equals(c));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a.GetHashCode(), c.GetHashCode());
    }

    [Fact]
    public void Equals_Negative()
    {
        var a = new RouteConfig()
        {
            AuthorizationPolicy = "a",
            RateLimiterPolicy = "rl",
            TimeoutPolicy = "t",
            Timeout = TimeSpan.FromSeconds(1),
            ClusterId = "c",
            CorsPolicy = "co",
            Match = new RouteMatch()
            {
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Hi",
                        Values = new[] { "v1", "v2" },
                        IsCaseSensitive = true,
                        Mode = HeaderMatchMode.HeaderPrefix,
                    }
                },
                Hosts = new[] { "foo:90" },
                Methods = new[] { "GET", "POST" },
                Path = "/p",
            },
            Metadata = new Dictionary<string, string>()
            {
                { "m", "m1" }
            },
            Order = 1,
            RouteId = "R",
        };
        var b = a with { AuthorizationPolicy = "b" };
        var c = a with { ClusterId = "d" };
        var d = a with { CorsPolicy = "p" };
        var e = a with { Match = new RouteMatch() };
        var f = a with { Metadata = new Dictionary<string, string>() { { "f", "f1" } } };
        var g = a with { Order = null };
        var h = a with { RouteId = "h" };
        var i = a with { RateLimiterPolicy = "i" };
        var j = a with { TimeoutPolicy = "j" };
        var k = a with { Timeout = TimeSpan.FromSeconds(107) };

        Assert.False(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(d));
        Assert.False(a.Equals(e));
        Assert.False(a.Equals(f));
        Assert.False(a.Equals(g));
        Assert.False(a.Equals(h));
        Assert.False(a.Equals(i));
        Assert.False(a.Equals(j));
        Assert.False(a.Equals(k));
    }

    [Fact]
    public void Equals_Null_False()
    {
        Assert.False(new RouteConfig().Equals(null));
    }

    [Fact]
    public void RouteConfig_CanBeJsonSerialized()
    {
        var route1 = new RouteConfig()
        {
            AuthorizationPolicy = "a",
            RateLimiterPolicy = "rl",
            ClusterId = "c",
            CorsPolicy = "co",
            Match = new RouteMatch()
            {
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Hi",
                        Values = new[] { "v1", "v2" },
                        IsCaseSensitive = true,
                        Mode = HeaderMatchMode.HeaderPrefix,
                    }
                },
                Hosts = new[] { "foo:90" },
                Methods = new[] { "GET", "POST" },
                Path = "/p",
            },
            Metadata = new Dictionary<string, string>()
            {
                { "m", "m1" }
            },
            Transforms = new[]
            {
                new Dictionary<string, string>
                {
                    { "key", "value" },
                    { "key1", "" }
                }
            },
            Order = 1,
            RouteId = "R",
        };

        var json = JsonSerializer.Serialize(route1);
        var route2 = JsonSerializer.Deserialize<RouteConfig>(json);

        Assert.Equal(route1, route2);
    }
}
