// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Configuration;

public class OutputCacheConfigTests
{
    [Fact]
    public async Task All_Options_Added()
    {
        var config = new OutputCacheConfig();
        config.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(1);
        config.MaximumBodySize = 10;
        config.SizeLimit = 20;
        config.UseCaseSensitivePaths = true;
        config.NamedPolicies.Add("test1", new NamedCacheConfig { Duration = TimeSpan.FromSeconds(5), ExcludeDefaultPolicy = true });
        config.NamedPolicies.Add("test2", new NamedCacheConfig { Duration = TimeSpan.FromSeconds(15), ExcludeDefaultPolicy = false });
        config.NamedPolicies.Add("test3", new NamedCacheConfig { Duration = TimeSpan.FromSeconds(3), ExcludeDefaultPolicy = true, VaryByHeaders = new[] { "X-SomeHeader" } });

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddOutputCache(config)
                        .AddReverseProxy();

        var app = builder.Build();

        var policies = app.Services.GetRequiredService<IYarpOutputCachePolicyProvider>();
        var test1 = await policies.GetPolicyAsync("test1");
        var test2 = await policies.GetPolicyAsync("test2");
        var test3 = await policies.GetPolicyAsync("test3");

        Assert.NotNull(test1);
        Assert.NotNull(test2);
        Assert.NotNull(test3);
    }

    [Fact]
    public async Task All_Options_Added_Json()
    {
        var json =
            """
            {
                "OutputCache": {
                    "DefaultExpirationTimeSpan": "00:05:00",
                    "MaximumBodySize": 10,
                    "SizeLimit": 20,
                    "UseCaseSensitivePaths": true,
                    "NamedPolicies": {
                        "test1": {
                            "Duration": "00:05:00",
                            "ExcludeDefaultPolicy": true
                        },
                        "test2": {
                            "Duration": "00:15:00",
                            "ExcludeDefaultPolicy": false
                        },
                        "test3": {
                            "Duration": "00:03:00",
                            "ExcludeDefaultPolicy": true,
                            "VaryByHeaders": [ "X-SomeHeader" ]
                        }
                    }
                }
            }
            """;
        var configBuilder = new ConfigurationBuilder();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = configBuilder.AddJsonStream(stream).Build();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddOutputCache(config.GetSection("OutputCache"))
                        .AddReverseProxy();

        var app = builder.Build();

        var policies = app.Services.GetRequiredService<IYarpOutputCachePolicyProvider>();
        var test1 = await policies.GetPolicyAsync("test1");
        var test2 = await policies.GetPolicyAsync("test2");
        var test3 = await policies.GetPolicyAsync("test3");

        Assert.NotNull(test1);
        Assert.NotNull(test2);
        Assert.NotNull(test3);
    }
}
