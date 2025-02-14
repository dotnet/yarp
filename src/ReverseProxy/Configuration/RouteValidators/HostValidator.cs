// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class HostValidator : IRouteValidator
{
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var route = routeConfig.Match;
        if (route.Hosts is null || route.Hosts.Count == 0)
        {
            // Host is optional when Path is specified
            return ValueTask.CompletedTask;
        }

        foreach (var host in route.Hosts)
        {
            if (string.IsNullOrEmpty(host))
            {
                errors.Add(new ArgumentException($"Empty host name has been set for route '{routeConfig.RouteId}'."));
            }
            else if (host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ArgumentException($"Punycode host name '{host}' has been set for route '{routeConfig.RouteId}'. Use the unicode host name instead."));
            }
        }

        return ValueTask.CompletedTask;
    }
}
