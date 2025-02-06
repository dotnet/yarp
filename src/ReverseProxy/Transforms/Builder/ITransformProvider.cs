// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Enables the implementor to inspect each route and conditionally add transforms.
/// </summary>
public interface ITransformProvider
{
    /// <summary>
    /// Validates any route data needed for transforms.
    /// </summary>
    /// <param name="context">The context to add any generated errors to.</param>
    void ValidateRoute(TransformRouteValidationContext context);

    /// <summary>
    /// Validates any cluster data needed for transforms.
    /// </summary>
    /// <param name="context">The context to add any generated errors to.</param>
    void ValidateCluster(TransformClusterValidationContext context);

    /// <summary>
    /// Inspect the given route and conditionally add transforms.
    /// This is called for every route, each time that route is built.
    /// </summary>
    /// <param name="context">The context to add any generated transforms to.</param>
    void Apply(TransformBuilderContext context);
}
