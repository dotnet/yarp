// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

/// <summary>
/// Provides method to validate route configuration.
/// </summary>
public interface IRouteValidator
{
    /// <summary>
    /// Perform validation on a route by adding exceptions to the provided collection.
    /// </summary>
    /// <param name="routeConfig">Route configuration to validate</param>
    /// <param name="errors">Collection of all validation exceptions</param>
    /// <returns>A ValueTask representing the asynchronous validation operation.</returns>
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors);
}
