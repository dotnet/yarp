// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Representation of a cluster for use at runtime.
/// </summary>
public sealed class ClusterState
{
    private volatile ClusterDestinationsState _destinationsState = new ClusterDestinationsState(Array.Empty<DestinationState>(), Array.Empty<DestinationState>());
    private volatile ClusterModel _model = default!; // Initialized right after construction.

    /// <summary>
    /// Creates a new instance. This constructor is for tests and infrastructure, this type is normally constructed by the configuration
    /// loading infrastructure.
    /// </summary>
    public ClusterState(string clusterId)
    {
        ArgumentNullException.ThrowIfNull(clusterId);
        ClusterId = clusterId;
    }

    /// <summary>
    /// Constructor overload to additionally initialize the <see cref="ClusterModel"/> for tests and infrastructure,
    /// such as updating the <see cref="ReverseProxyFeature"/> via <see cref="HttpContextFeaturesExtensions"/>
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public ClusterState(string clusterId, ClusterModel model) : this(clusterId)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>
    /// The cluster's unique id.
    /// </summary>
    public string ClusterId { get; }

    /// <summary>
    /// Encapsulates parts of a cluster that can change atomically in reaction to config changes.
    /// </summary>
    public ClusterModel Model
    {
        get => _model;
        internal set => _model = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// All the destinations associated with this cluster. This collection is populated by the configuration system
    /// and should only be directly modified in a test environment.
    /// Call <see cref="Health.IClusterDestinationsUpdater"/> after modifying this collection.
    /// </summary>
    public ConcurrentDictionary<string, DestinationState> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stores the state of cluster's destinations that can change atomically
    /// in reaction to runtime state changes (e.g. changes of destinations' health).
    /// </summary>
    public ClusterDestinationsState DestinationsState
    {
        get => _destinationsState;
        set => _destinationsState = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Keeps track of the total number of concurrent requests on this cluster.
    /// </summary>
    internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

    /// <summary>
    /// Tracks changes to the cluster configuration for use with rebuilding dependent endpoints. Destination changes do not affect this property.
    /// </summary>
    internal int Revision { get; set; }
}
