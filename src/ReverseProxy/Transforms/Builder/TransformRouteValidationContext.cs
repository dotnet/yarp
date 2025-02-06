// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// State used when validating transforms for the given route.
/// </summary>
public class TransformRouteValidationContext
{
    /// <summary>
    /// Application services that can be used to validate transforms.
    /// </summary>
    public IServiceProvider Services { get; init; } = default!;

    /// <summary>
    /// The route these transforms are associated with.
    /// </summary>
    public RouteConfig Route { get; init; } = default!;

    /// <summary>
    /// The accumulated list of validation errors for this route.
    /// Add transform validation errors here.
    /// </summary>
    public IList<Exception> Errors { get; } = new List<Exception>();
}
