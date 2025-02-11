// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class MethodsValidator : IRouteValidator
{
    private static readonly HashSet<string> _validMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
    };

    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var route = routeConfig.Match;
        if (route.Methods is null)
        {
            // Methods are optional
            return ValueTask.CompletedTask;
        }

        var seenMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in route.Methods)
        {
            if (!seenMethods.Add(method))
            {
                errors.Add(new ArgumentException($"Duplicate HTTP method '{method}' for route '{routeConfig.RouteId}'."));
                continue;
            }

            if (!_validMethods.Contains(method))
            {
                errors.Add(new ArgumentException($"Unsupported HTTP method '{method}' has been set for route '{routeConfig.RouteId}'."));
            }
        }

        return ValueTask.CompletedTask;
    }
}
