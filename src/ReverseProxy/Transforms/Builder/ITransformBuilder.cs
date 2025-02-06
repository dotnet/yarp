// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Validates and builds request and response transforms for a given route.
/// </summary>
public interface ITransformBuilder
{
    /// <summary>
    /// Validates that each transform for the given route is known and has the expected parameters. All transforms are validated
    /// so all errors can be reported.
    /// </summary>
    IReadOnlyList<Exception> ValidateRoute(RouteConfig route);

    /// <summary>
    /// Validates that any cluster data needed for transforms is valid.
    /// </summary>
    IReadOnlyList<Exception> ValidateCluster(ClusterConfig cluster);

    /// <summary>
    /// Builds the transforms for the given route into executable rules.
    /// </summary>
    HttpTransformer Build(RouteConfig route, ClusterConfig? cluster);

    HttpTransformer Create(Action<TransformBuilderContext> action);
}
