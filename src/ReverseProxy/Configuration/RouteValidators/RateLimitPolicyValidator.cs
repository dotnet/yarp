// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class RateLimitPolicyValidator : IRouteValidator
{
    private readonly IYarpRateLimiterPolicyProvider _rateLimiterPolicyProvider;

    public RateLimitPolicyValidator(IYarpRateLimiterPolicyProvider rateLimiterPolicyProvider)
    {
        _rateLimiterPolicyProvider = rateLimiterPolicyProvider;
    }

    public async ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var rateLimiterPolicyName = routeConfig.RateLimiterPolicy;

        if (string.IsNullOrEmpty(rateLimiterPolicyName))
        {
            return;
        }

        if (string.Equals(RateLimitingConstants.Default, rateLimiterPolicyName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(RateLimitingConstants.Disable, rateLimiterPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var policy = await _rateLimiterPolicyProvider.GetPolicyAsync(rateLimiterPolicyName);
            if (policy is not null)
            {
                // We weren't expecting to find a policy with these names.
                errors.Add(new ArgumentException(
                    $"The application has registered a RateLimiter policy named '{rateLimiterPolicyName}' that conflicts with the reserved RateLimiter policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }

            return;
        }

        try
        {
            var policy = await _rateLimiterPolicyProvider.GetPolicyAsync(rateLimiterPolicyName);

            if (policy is null)
            {
                errors.Add(new ArgumentException(
                    $"RateLimiter policy '{rateLimiterPolicyName}' not found for route '{routeConfig.RouteId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException(
                $"Unable to retrieve the RateLimiter policy '{rateLimiterPolicyName}' for route '{routeConfig.RouteId}'.",
                ex));
        }
    }
}
