// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class OutputCachePolicyValidator : IRouteValidator
{
#if NET7_0_OR_GREATER
    private readonly IYarpOutputCachePolicyProvider _outputCachePolicyProvider;
    public OutputCachePolicyValidator(IYarpOutputCachePolicyProvider outputCachePolicyProvider)
    {
        _outputCachePolicyProvider = outputCachePolicyProvider;
    }

    public async ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var outputCachePolicyName = routeConfig.OutputCachePolicy;

        if (string.IsNullOrEmpty(outputCachePolicyName))
        {
            return;
        }

        try
        {
            var policy = await _outputCachePolicyProvider.GetPolicyAsync(outputCachePolicyName);

            if (policy is null)
            {
                errors.Add(new ArgumentException(
                    $"OutputCache policy '{outputCachePolicyName}' not found for route '{routeConfig.RouteId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException(
                $"Unable to retrieve the OutputCache policy '{outputCachePolicyName}' for route '{routeConfig.RouteId}'.",
                ex));
        }
    }
#else
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors) => ValueTask.CompletedTask;
#endif
}
