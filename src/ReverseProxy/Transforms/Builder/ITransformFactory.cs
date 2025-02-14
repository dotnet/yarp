// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Validates and builds transforms from the given parameters
/// </summary>
public interface ITransformFactory
{
    /// <summary>
    /// Checks if the given transform values match a known transform, and if those values have any errors.
    /// </summary>
    /// <param name="context">The context to add any generated errors to.</param>
    /// <param name="transformValues">The transform values to validate.</param>
    /// <returns>True if this factory matches the given transform, otherwise false.</returns>
    bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues);

    /// <summary>
    /// Checks if the given transform values match a known transform, and if so, generates a transform and
    /// adds it to the context. This can throw if the transform values are invalid.
    /// </summary>
    /// <param name="context">The context to add any generated transforms to.</param>
    /// <param name="transformValues">The transform values to use as input.</param>
    /// <returns>True if this factory matches the given transform, otherwise false.</returns>
    bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues);
}
