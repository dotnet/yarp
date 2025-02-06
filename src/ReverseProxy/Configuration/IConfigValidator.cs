// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Provides methods to validate routes and clusters.
/// </summary>
public interface IConfigValidator
{
    /// <summary>
    /// Validates a route and returns all errors
    /// </summary>
    ValueTask<IList<Exception>> ValidateRouteAsync(RouteConfig route);

    /// <summary>
    /// Validates a cluster and returns all errors.
    /// </summary>
    ValueTask<IList<Exception>> ValidateClusterAsync(ClusterConfig cluster);
}
