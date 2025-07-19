// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration;

// TODO: update or remove this once AspNetCore provides a mechanism to validate the RateLimiter policies https://github.com/dotnet/aspnetcore/issues/45684

internal interface IYarpRateLimiterPolicyProvider
{
    ValueTask<object?> GetPolicyAsync(string policyName);
}

internal sealed class YarpRateLimiterPolicyProvider : IYarpRateLimiterPolicyProvider
{
    private readonly RateLimiterOptions _rateLimiterOptions;

    private readonly IDictionary _policyMap, _unactivatedPolicyMap;

    public YarpRateLimiterPolicyProvider(IOptions<RateLimiterOptions> rateLimiterOptions)
    {
        ArgumentNullException.ThrowIfNull(rateLimiterOptions?.Value);
        _rateLimiterOptions = rateLimiterOptions.Value;

        var type = typeof(RateLimiterOptions);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _policyMap = type.GetProperty("PolicyMap", flags)?.GetValue(_rateLimiterOptions, null) as IDictionary
            ?? throw new NotSupportedException("This version of YARP is incompatible with the current version of ASP.NET Core.");
        _unactivatedPolicyMap = type.GetProperty("UnactivatedPolicyMap", flags)?.GetValue(_rateLimiterOptions, null) as IDictionary
            ?? throw new NotSupportedException("This version of YARP is incompatible with the current version of ASP.NET Core.");
    }

    public ValueTask<object?> GetPolicyAsync(string policyName)
    {
        return ValueTask.FromResult(_policyMap[policyName] ?? _unactivatedPolicyMap[policyName]);
    }
}
