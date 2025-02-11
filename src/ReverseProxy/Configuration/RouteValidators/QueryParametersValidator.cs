// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class QueryParametersValidator : IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var route = routeConfig.Match;
        if (route.QueryParameters is null)
        {
            // Query Parameters are optional
            return ValueTask.CompletedTask;
        }

        foreach (var queryParameter in route.QueryParameters)
        {
            if (queryParameter is null)
            {
                errors.Add(new ArgumentException($"A null route query parameter has been set for route '{routeConfig.RouteId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(queryParameter.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route query parameter name has been set for route '{routeConfig.RouteId}'."));
            }

            if (queryParameter.Mode != QueryParameterMatchMode.Exists
                && (queryParameter.Values is null || queryParameter.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No query parameter values were set on route query parameter '{queryParameter.Name}' for route '{routeConfig.RouteId}'."));
            }

            if (queryParameter.Mode == QueryParameterMatchMode.Exists && queryParameter.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Query parameter values where set when using mode '{nameof(QueryParameterMatchMode.Exists)}' on route query parameter '{queryParameter.Name}' for route '{routeConfig.RouteId}'."));
            }
        }

        return ValueTask.CompletedTask;
    }
}
