// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class AuthorizationPolicyValidator
    (IAuthorizationPolicyProvider authorizationPolicyProvider) : IRouteValidator
{
    public async ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var authorizationPolicyName = routeConfig.AuthorizationPolicy;
        if (string.IsNullOrEmpty(authorizationPolicyName))
        {
            return;
        }

        if (string.Equals(AuthorizationConstants.Default, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var policy = await authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException($"The application has registered an authorization policy named '{authorizationPolicyName}' that conflicts with the reserved authorization policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }

            return;
        }

        if (string.Equals(AuthorizationConstants.Anonymous, authorizationPolicyName,
                StringComparison.OrdinalIgnoreCase))
        {
            var policy = await authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException(
                    $"The application has registered an authorization policy named '{authorizationPolicyName}' that conflicts with the reserved authorization policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }

            return;
        }

        try
        {
            var policy = await authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is null)
            {
                errors.Add(new ArgumentException(
                    $"Authorization policy '{authorizationPolicyName}' not found for route '{routeConfig.RouteId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException(
                $"Unable to retrieve the authorization policy '{authorizationPolicyName}' for route '{routeConfig.RouteId}'.", ex));
        }
    }
}
