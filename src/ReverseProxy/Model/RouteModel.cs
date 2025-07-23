// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Immutable representation of the portions of a route
/// that only change in reaction to configuration changes.
/// </summary>
/// <remarks>
/// All members must remain immutable to avoid thread safety issues.
/// Instead, instances of <see cref="RouteModel"/> are replaced
/// in their entirety when values need to change.
/// </remarks>
public sealed class RouteModel
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public RouteModel(
        RouteConfig config,
        ClusterState? cluster,
        HttpTransformer transformer)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(transformer);

        Config = config;
        Cluster = cluster;
        Transformer = transformer;
    }

    // May not be populated if the cluster config is missing. https://github.com/dotnet/yarp/issues/797
    /// <summary>
    /// The <see cref="ClusterState"/> instance associated with this route.
    /// </summary>
    public ClusterState? Cluster { get; }

    /// <summary>
    /// Transforms to apply for this route.
    /// </summary>
    public HttpTransformer Transformer { get; }

    /// <summary>
    /// The configuration data used to build this route.
    /// </summary>
    public RouteConfig Config { get; }

    internal bool HasConfigChanged(RouteConfig newConfig, ClusterState? cluster, int? routeRevision)
    {
        return Cluster != cluster || routeRevision != cluster?.Revision || !Config.Equals(newConfig);
    }
}
