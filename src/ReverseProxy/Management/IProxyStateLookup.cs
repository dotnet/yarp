// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy;

/// <summary>
/// Allows access to the proxy's current set of routes and clusters.
/// </summary>
public interface IProxyStateLookup
{
    /// <summary>
    /// Retrieves a specific route by id, if present.
    /// </summary>
    bool TryGetRoute(string id, [NotNullWhen(true)] out RouteModel? route);

    /// <summary>
    /// Enumerates all current routes. This is thread safe but the collection may change mid-enumeration if the configuration is reloaded.
    /// </summary>
    IEnumerable<RouteModel> GetRoutes();

    /// <summary>
    /// Retrieves a specific cluster by id, if present.
    /// </summary>
    bool TryGetCluster(string id, [NotNullWhen(true)] out ClusterState? cluster);

    /// <summary>
    /// Enumerates all current clusters. This is thread safe but the collection may change mid-enumeration if the configuration is reloaded.
    /// </summary>
    IEnumerable<ClusterState> GetClusters();
}
