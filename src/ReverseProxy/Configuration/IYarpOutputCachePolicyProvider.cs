// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET7_0_OR_GREATER
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
#endif

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration;

// TODO: update or remove this once AspNetCore provides a mechanism to validate the OutputCache policies https://github.com/dotnet/aspnetcore/issues/52419

internal interface IYarpOutputCachePolicyProvider
{
    ValueTask<object?> GetPolicyAsync(string policyName);
}

internal sealed class YarpOutputCachePolicyProvider : IYarpOutputCachePolicyProvider
{
#if NET7_0_OR_GREATER
    // Workaround for https://github.com/dotnet/yarp/issues/2598 to make YARP work with NativeAOT on .NET 8. This is not needed on .NET 9+.
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static readonly Type s_OutputCacheOptionsType = typeof(OutputCacheOptions);

    private readonly OutputCacheOptions _outputCacheOptions;

    private readonly IDictionary _policyMap;

    public YarpOutputCachePolicyProvider(IOptions<OutputCacheOptions> outputCacheOptions)
    {
        _outputCacheOptions = outputCacheOptions?.Value ?? throw new ArgumentNullException(nameof(outputCacheOptions));

        var property = s_OutputCacheOptionsType.GetProperty("NamedPolicies", BindingFlags.Instance | BindingFlags.NonPublic);
        if (property == null || !typeof(IDictionary).IsAssignableFrom(property.PropertyType))
        {
            throw new NotSupportedException("This version of YARP is incompatible with the current version of ASP.NET Core.");
        }
        _policyMap = (property.GetValue(_outputCacheOptions, null) as IDictionary) ?? new Dictionary<string, object>();
    }

    public ValueTask<object?> GetPolicyAsync(string policyName)
    {
        return ValueTask.FromResult(_policyMap[policyName]);
    }
#else
    public ValueTask<object?> GetPolicyAsync(string policyName)
    {
        return default;
    }
#endif
}
