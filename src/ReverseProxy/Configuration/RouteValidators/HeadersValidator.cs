// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class HeadersValidator : IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var route = routeConfig.Match;
        if (route.Headers is null)
        {
            // Headers are optional
            return ValueTask.CompletedTask;
        }

        foreach (var header in route.Headers)
        {
            if (header is null)
            {
                errors.Add(new ArgumentException($"A null route header has been set for route '{routeConfig.RouteId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(header.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route header name has been set for route '{routeConfig.RouteId}'."));
            }

            if (header.Mode != HeaderMatchMode.Exists && header.Mode != HeaderMatchMode.NotExists
                                                      && (header.Values is null || header.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No header values were set on route header '{header.Name}' for route '{routeConfig.RouteId}'."));
            }

            if ((header.Mode == HeaderMatchMode.Exists || header.Mode == HeaderMatchMode.NotExists) && header.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Header values were set when using mode '{header.Mode}' on route header '{header.Name}' for route '{routeConfig.RouteId}'."));
            }
        }

        return ValueTask.CompletedTask;
    }
}
