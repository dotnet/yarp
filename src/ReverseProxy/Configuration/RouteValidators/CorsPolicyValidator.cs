// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class CorsPolicyValidator(ICorsPolicyProvider corsPolicyProvider) : IRouteValidator
{
    public async ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var corsPolicyName = routeConfig.CorsPolicy;
        if (string.IsNullOrEmpty(corsPolicyName))
        {
            return;
        }

        if (string.Equals(CorsConstants.Default, corsPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException(
                    $"The application has registered a CORS policy named '{corsPolicyName}' that conflicts with the reserved CORS policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }

            return;
        }

        if (string.Equals(CorsConstants.Disable, corsPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException(
                    $"The application has registered a CORS policy named '{corsPolicyName}' that conflicts with the reserved CORS policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }

            return;
        }

        try
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is null)
            {
                errors.Add(new ArgumentException(
                    $"CORS policy '{corsPolicyName}' not found for route '{routeConfig.RouteId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException(
                $"Unable to retrieve the CORS policy '{corsPolicyName}' for route '{routeConfig.RouteId}'.", ex));
        }
    }
}
