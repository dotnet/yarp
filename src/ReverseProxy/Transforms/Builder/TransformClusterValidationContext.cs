// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// State used when validating transforms for the given cluster.
/// </summary>
public class TransformClusterValidationContext
{
    /// <summary>
    /// Application services that can be used to validate transforms.
    /// </summary>
    public IServiceProvider Services { get; init; } = default!;

    /// <summary>
    /// The cluster configuration that may be used when creating transforms.
    /// </summary>
    public ClusterConfig Cluster { get; init; } = default!;

    /// <summary>
    /// The accumulated list of validation errors for this cluster.
    /// Add validation errors here.
    /// </summary>
    public IList<Exception> Errors { get; } = new List<Exception>();
}
