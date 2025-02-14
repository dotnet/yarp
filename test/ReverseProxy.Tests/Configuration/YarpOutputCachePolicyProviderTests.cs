// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Configuration;

public class YarpOutputCachePolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_Works()
    {
        var services = new ServiceCollection();

        services.AddOutputCache(options =>
        {
            options.AddPolicy("customPolicy", opt =>
            {
                opt.Expire(TimeSpan.FromSeconds(12));
                opt.SetVaryByHost(true);
            });
        });

        services.AddReverseProxy();
        var provider = services.BuildServiceProvider();
        var outputCachePolicyProvider = provider.GetRequiredService<IYarpOutputCachePolicyProvider>();
        Assert.Null(await outputCachePolicyProvider.GetPolicyAsync("anotherPolicy"));
        Assert.NotNull(await outputCachePolicyProvider.GetPolicyAsync("customPolicy"));
    }
}
#endif
