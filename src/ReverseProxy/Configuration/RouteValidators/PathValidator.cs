// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class PathValidator : IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var route = routeConfig.Match;
        if (string.IsNullOrEmpty(route.Path))
        {
            // Path is optional when Host is specified
            return ValueTask.CompletedTask;
        }

        try
        {
            RoutePatternFactory.Parse(route.Path);
        }
        catch (RoutePatternException ex)
        {
            errors.Add(new ArgumentException($"Invalid path '{route.Path}' for route '{routeConfig.RouteId}'.", ex));
        }

        return ValueTask.CompletedTask;
    }
}
