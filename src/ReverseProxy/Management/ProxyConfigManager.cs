// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.ServiceDiscovery;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Management;

/// <summary>
/// Provides a method to apply Proxy configuration changes.
/// Also an Implementation of <see cref="EndpointDataSource"/> that supports being dynamically updated
/// in a thread-safe manner while avoiding locks on the hot path.
/// </summary>
// https://github.com/dotnet/aspnetcore/blob/cbe16474ce9db7ff588aed89596ff4df5c3f62e1/src/Mvc/Mvc.Core/src/Routing/ActionEndpointDataSourceBase.cs
internal sealed class ProxyConfigManager : EndpointDataSource, IProxyStateLookup, IDisposable
{
    private static readonly IReadOnlyDictionary<string, ClusterConfig> _emptyClusterDictionary = new ReadOnlyDictionary<string, ClusterConfig>(new Dictionary<string, ClusterConfig>());

    private readonly object _syncRoot = new();
    private readonly ILogger<ProxyConfigManager> _logger;
    private readonly IProxyConfigProvider[] _providers;
    private readonly ConfigState[] _configs;
    private readonly IClusterChangeListener[] _clusterChangeListeners;
    private readonly ConcurrentDictionary<string, ClusterState> _clusters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RouteState> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProxyConfigFilter[] _filters;
    private readonly IConfigValidator _configValidator;
    private readonly IForwarderHttpClientFactory _httpClientFactory;
    private readonly ProxyEndpointFactory _proxyEndpointFactory;
    private readonly ITransformBuilder _transformBuilder;
    private readonly List<Action<EndpointBuilder>> _conventions;
    private readonly IActiveHealthCheckMonitor _activeHealthCheckMonitor;
    private readonly IClusterDestinationsUpdater _clusterDestinationsUpdater;
    private readonly IDestinationResolver _destinationResolver;
    private readonly IConfigChangeListener[] _configChangeListeners;
    private List<Endpoint>? _endpoints;
    private CancellationTokenSource _endpointsChangeSource = new();
    private IChangeToken _endpointsChangeToken;

    private CancellationTokenSource _configChangeSource = new();

    public ProxyConfigManager(
        ILogger<ProxyConfigManager> logger,
        IEnumerable<IProxyConfigProvider> providers,
        IEnumerable<IClusterChangeListener> clusterChangeListeners,
        IEnumerable<IProxyConfigFilter> filters,
        IConfigValidator configValidator,
        ProxyEndpointFactory proxyEndpointFactory,
        ITransformBuilder transformBuilder,
        IForwarderHttpClientFactory httpClientFactory,
        IActiveHealthCheckMonitor activeHealthCheckMonitor,
        IClusterDestinationsUpdater clusterDestinationsUpdater,
        IEnumerable<IConfigChangeListener> configChangeListeners,
        IDestinationResolver destinationResolver)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(clusterChangeListeners);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(configValidator);
        ArgumentNullException.ThrowIfNull(proxyEndpointFactory);
        ArgumentNullException.ThrowIfNull(transformBuilder);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(activeHealthCheckMonitor);
        ArgumentNullException.ThrowIfNull(clusterDestinationsUpdater);
        ArgumentNullException.ThrowIfNull(configChangeListeners);
        ArgumentNullException.ThrowIfNull(destinationResolver);

        _logger = logger;
        _providers = providers.ToArray();
        _clusterChangeListeners = clusterChangeListeners.ToArray();
        _filters = filters.ToArray();
        _configValidator = configValidator;
        _proxyEndpointFactory = proxyEndpointFactory;
        _transformBuilder = transformBuilder;
        _httpClientFactory = httpClientFactory;
        _activeHealthCheckMonitor = activeHealthCheckMonitor;
        _clusterDestinationsUpdater = clusterDestinationsUpdater;
        _destinationResolver = destinationResolver;
        _configChangeListeners = configChangeListeners.ToArray();

        if (_providers.Length == 0)
        {
            throw new ArgumentException($"At least one {nameof(IProxyConfigProvider)} is required.", nameof(providers));
        }

        _configs = new ConfigState[_providers.Length];

        _conventions = new List<Action<EndpointBuilder>>();
        DefaultBuilder = new ReverseProxyConventionBuilder(_conventions);

        _endpointsChangeToken = new CancellationChangeToken(_endpointsChangeSource.Token);
    }

    public ReverseProxyConventionBuilder DefaultBuilder { get; }

    // EndpointDataSource

    /// <inheritdoc/>
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            // The Endpoints needs to be lazy the first time to give a chance to ReverseProxyConventionBuilder to add its conventions.
            // Endpoints are accessed by routing on the first request.
            if (_endpoints is null)
            {
                lock (_syncRoot)
                {
                    if (_endpoints is null)
                    {
                        CreateEndpoints();
                    }
                }
            }
            return _endpoints;
        }
    }

    [MemberNotNull(nameof(_endpoints))]
    private void CreateEndpoints()
    {
        var endpoints = new List<Endpoint>();
        // Directly enumerate the ConcurrentDictionary to limit locking and copying.
        foreach (var existingRoute in _routes)
        {
            // Only rebuild the endpoint for modified routes or clusters.
            var endpoint = existingRoute.Value.CachedEndpoint;
            if (endpoint is null)
            {
                endpoint = _proxyEndpointFactory.CreateEndpoint(existingRoute.Value.Model, _conventions);
                existingRoute.Value.CachedEndpoint = endpoint;
            }
            endpoints.Add(endpoint);
        }

        UpdateEndpoints(endpoints);
    }

    /// <inheritdoc/>
    public override IChangeToken GetChangeToken() => Volatile.Read(ref _endpointsChangeToken);

    private static IProxyConfig[] ExtractListOfProxyConfigs(IEnumerable<ConfigState> configStates)
    {
        return configStates.Select(state => state.LatestConfig).ToArray();
    }

    internal async Task<EndpointDataSource> InitialLoadAsync()
    {
        // Trigger the first load immediately and throw if it fails.
        // We intend this to crash the app, so we don't try listening for further changes.
        try
        {
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            // Begin resolving config providers concurrently.
            var resolvedConfigs = new List<(int Index, IProxyConfigProvider Provider, ValueTask<IProxyConfig> Config)>(_providers.Length);
            for (var i = 0; i < _providers.Length; i++)
            {
                var provider = _providers[i];
                var configLoadTask = LoadConfigAsync(provider, cancellationToken: default);
                resolvedConfigs.Add((i, provider, configLoadTask));
            }

            // Wait for all configs to be resolved.
            foreach (var (i, provider, configLoadTask) in resolvedConfigs)
            {
                var config = await configLoadTask;
                _configs[i] = new ConfigState(provider, config);
                routes.AddRange(config.Routes ?? Array.Empty<RouteConfig>());
                clusters.AddRange(config.Clusters ?? Array.Empty<ClusterConfig>());
            }

            var proxyConfigs = ExtractListOfProxyConfigs(_configs);

            foreach (var configChangeListener in _configChangeListeners)
            {
                configChangeListener.ConfigurationLoaded(proxyConfigs);
            }

            await ApplyConfigAsync(routes, clusters);

            foreach (var configChangeListener in _configChangeListeners)
            {
                configChangeListener.ConfigurationApplied(proxyConfigs);
            }

            ListenForConfigChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to load or apply the proxy configuration.", ex);
        }

        // Initial active health check is run in the background.
        // Directly enumerate the ConcurrentDictionary to limit locking and copying.
        _ = _activeHealthCheckMonitor.CheckHealthAsync(_clusters.Select(pair => pair.Value));
        return this;
    }

    private async Task ReloadConfigAsync()
    {
        _configChangeSource.Dispose();

        var sourcesChanged = false;
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        var reloadedConfigs = new List<(ConfigState Config, ValueTask<IProxyConfig> ResolveTask)>();

        // Start reloading changed configurations.
        foreach (var instance in _configs)
        {
            if (instance.LatestConfig.ChangeToken.HasChanged)
            {
                try
                {
                    var reloadTask = LoadConfigAsync(instance.Provider, cancellationToken: default);
                    reloadedConfigs.Add((instance, reloadTask));
                }
                catch (Exception ex)
                {
                    OnConfigLoadError(instance, ex);
                }
            }
        }

        // Wait for all changed config providers to be reloaded.
        foreach (var (instance, loadTask) in reloadedConfigs)
        {
            try
            {
                instance.LatestConfig = await loadTask.ConfigureAwait(false);
                instance.LoadFailed = false;
                sourcesChanged = true;
            }
            catch (Exception ex)
            {
                OnConfigLoadError(instance, ex);
            }
        }

        // Extract the routes and clusters from the configs, regardless of whether they were reloaded.
        foreach (var instance in _configs)
        {
            if (instance.LatestConfig.Routes is { Count: > 0 } updatedRoutes)
            {
                routes.AddRange(updatedRoutes);
            }

            if (instance.LatestConfig.Clusters is { Count: > 0 } updatedClusters)
            {
                clusters.AddRange(updatedClusters);
            }
        }

        var proxyConfigs = ExtractListOfProxyConfigs(_configs);
        foreach (var configChangeListener in _configChangeListeners)
        {
            configChangeListener.ConfigurationLoaded(proxyConfigs);
        }

        try
        {
            // Only reload if at least one provider changed.
            if (sourcesChanged)
            {
                var hasChanged = await ApplyConfigAsync(routes, clusters);
                lock (_syncRoot)
                {
                    // Skip if changes are signaled before the endpoints are initialized for the first time.
                    // The endpoint conventions might not be ready yet.
                    if (hasChanged && _endpoints is not null)
                    {
                        CreateEndpoints();
                    }
                }
            }

            foreach (var configChangeListener in _configChangeListeners)
            {
                configChangeListener.ConfigurationApplied(proxyConfigs);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorApplyingConfig(_logger, ex);

            foreach (var configChangeListener in _configChangeListeners)
            {
                configChangeListener.ConfigurationApplyingFailed(proxyConfigs, ex);
            }
        }

        ListenForConfigChanges();

        void OnConfigLoadError(ConfigState instance, Exception ex)
        {
            instance.LoadFailed = true;
            Log.ErrorReloadingConfig(_logger, ex);

            foreach (var configChangeListener in _configChangeListeners)
            {
                configChangeListener.ConfigurationLoadingFailed(instance.Provider, ex);
            }
        }
    }

    private static void ValidateConfigProperties(IProxyConfig config)
    {
        if (config is null)
        {
            throw new InvalidOperationException($"{nameof(IProxyConfigProvider.GetConfig)} returned a null value.");
        }

        if (config.ChangeToken is null)
        {
            throw new InvalidOperationException($"{nameof(IProxyConfig.ChangeToken)} has a null value.");
        }
    }

    private ValueTask<IProxyConfig> LoadConfigAsync(IProxyConfigProvider provider, CancellationToken cancellationToken)
    {
        var config = provider.GetConfig();
        ValidateConfigProperties(config);

        if (_destinationResolver.GetType() == typeof(NoOpDestinationResolver))
        {
            return new(config);
        }

        return LoadConfigAsyncCore(config, cancellationToken);
    }

    private async ValueTask<IProxyConfig> LoadConfigAsyncCore(IProxyConfig config, CancellationToken cancellationToken)
    {
        List<(int Index, ValueTask<ResolvedDestinationCollection> Task)> resolverTasks = new();
        List<ClusterConfig> clusters = new(config.Clusters);
        List<IChangeToken>? changeTokens = null;
        for (var i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            if (cluster.Destinations is { Count: > 0 } destinations)
            {
                // Resolve destinations if there are any.
                var task = _destinationResolver.ResolveDestinationsAsync(destinations, cancellationToken);
                resolverTasks.Add((i, task));
            }
        }

        if (resolverTasks.Count > 0)
        {
            foreach (var (i, task) in resolverTasks)
            {
                ResolvedDestinationCollection resolvedDestinations;
                try
                {
                    resolvedDestinations = await task;
                }
                catch (Exception exception)
                {
                    var cluster = clusters[i];
                    throw new InvalidOperationException($"Error resolving destinations for cluster {cluster.ClusterId}", exception);
                }

                clusters[i] = clusters[i] with { Destinations = resolvedDestinations.Destinations };
                if (resolvedDestinations.ChangeToken is { } token)
                {
                    changeTokens ??= new();
                    changeTokens.Add(token);
                }
            }

            IChangeToken changeToken;
            if (changeTokens is not null)
            {
                // Combine change tokens from the resolver with the configuration's existing change token.
                changeTokens.Add(config.ChangeToken);
                changeToken = new CompositeChangeToken(changeTokens);
            }
            else
            {
                changeToken = config.ChangeToken;
            }

            // Return updated config
            return new ResolvedProxyConfig(config, clusters, changeToken);
        }

        return config;
    }

    private sealed class ResolvedProxyConfig : IProxyConfig
    {
        private readonly IProxyConfig _innerConfig;

        public ResolvedProxyConfig(IProxyConfig innerConfig, IReadOnlyList<ClusterConfig> clusters, IChangeToken changeToken)
        {
            _innerConfig = innerConfig;
            Clusters = clusters;
            ChangeToken = changeToken;
        }

        public IReadOnlyList<RouteConfig> Routes => _innerConfig.Routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }
    }

    private void ListenForConfigChanges()
    {
        // Use a central change token to avoid overlap between different sources.
        var source = new CancellationTokenSource();
        _configChangeSource = source;
        var poll = false;

        foreach (var configState in _configs)
        {
            if (configState.LoadFailed)
            {
                // We can't register for change notifications if the last load failed.
                poll = true;
                continue;
            }

            configState.CallbackCleanup?.Dispose();
            var token = configState.LatestConfig.ChangeToken;
            if (token.ActiveChangeCallbacks)
            {
                configState.CallbackCleanup = token.RegisterChangeCallback(SignalChange, source);
            }
            else
            {
                poll = true;
            }
        }

        if (poll)
        {
            source.CancelAfter(TimeSpan.FromMinutes(5));
        }

        // Don't register until we're done hooking everything up to avoid cancellation races.
        source.Token.Register(ReloadConfig, this);

        static void SignalChange(object? obj)
        {
            var token = (CancellationTokenSource)obj!;
            try
            {
                token.Cancel();
            }
            // Don't throw if the source was already disposed.
            catch (ObjectDisposedException) { }
        }

        static void ReloadConfig(object? state)
        {
            var manager = (ProxyConfigManager)state!;
            _ = manager.ReloadConfigAsync();
        }
    }

    // Throws for validation failures
    private async Task<bool> ApplyConfigAsync(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var (configuredClusters, clusterErrors) = await VerifyClustersAsync(clusters, cancellation: default);
        var (configuredRoutes, routeErrors) = await VerifyRoutesAsync(routes, configuredClusters, cancellation: default);

        if (routeErrors.Count > 0 || clusterErrors.Count > 0)
        {
            throw new AggregateException("The proxy config is invalid.", routeErrors.Concat(clusterErrors));
        }

        // Update clusters first because routes need to reference them.
        UpdateRuntimeClusters(configuredClusters);
        var routesChanged = UpdateRuntimeRoutes(configuredRoutes);
        return routesChanged;
    }

    private async Task<(IList<RouteConfig>, IList<Exception>)> VerifyRoutesAsync(IReadOnlyList<RouteConfig> routes, IReadOnlyDictionary<string, ClusterConfig> clusters, CancellationToken cancellation)
    {
        if (routes is null)
        {
            return (Array.Empty<RouteConfig>(), Array.Empty<Exception>());
        }

        var seenRouteIds = new HashSet<string>(routes.Count, StringComparer.OrdinalIgnoreCase);
        var configuredRoutes = new List<RouteConfig>(routes.Count);
        var errors = new List<Exception>();

        foreach (var r in routes)
        {
            if (seenRouteIds.Contains(r.RouteId))
            {
                errors.Add(new ArgumentException($"Duplicate route '{r.RouteId}'"));
                continue;
            }

            var route = r;

            try
            {
                if (_filters.Length != 0)
                {
                    ClusterConfig? cluster = null;
                    if (route.ClusterId is not null)
                    {
                        clusters.TryGetValue(route.ClusterId, out cluster);
                    }

                    foreach (var filter in _filters)
                    {
                        route = await filter.ConfigureRouteAsync(route, cluster, cancellation);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new Exception($"An exception was thrown from the configuration callbacks for route '{r.RouteId}'.", ex));
                continue;
            }

            var routeErrors = await _configValidator.ValidateRouteAsync(route);
            if (routeErrors.Count > 0)
            {
                errors.AddRange(routeErrors);
                continue;
            }

            seenRouteIds.Add(route.RouteId);
            configuredRoutes.Add(route);
        }

        if (errors.Count > 0)
        {
            return (Array.Empty<RouteConfig>(), errors);
        }

        return (configuredRoutes, errors);
    }

    private async Task<(IReadOnlyDictionary<string, ClusterConfig>, IList<Exception>)> VerifyClustersAsync(IReadOnlyList<ClusterConfig> clusters, CancellationToken cancellation)
    {
        if (clusters is null)
        {
            return (_emptyClusterDictionary, Array.Empty<Exception>());
        }

        var configuredClusters = new Dictionary<string, ClusterConfig>(clusters.Count, StringComparer.OrdinalIgnoreCase);
        var errors = new List<Exception>();
        // The IProxyConfigProvider provides a fresh snapshot that we need to reconfigure each time.
        foreach (var c in clusters)
        {
            try
            {
                if (configuredClusters.ContainsKey(c.ClusterId))
                {
                    errors.Add(new ArgumentException($"Duplicate cluster '{c.ClusterId}'."));
                    continue;
                }

                // Don't modify the original
                var cluster = c;

                foreach (var filter in _filters)
                {
                    cluster = await filter.ConfigureClusterAsync(cluster, cancellation);
                }

                var clusterErrors = await _configValidator.ValidateClusterAsync(cluster);
                if (clusterErrors.Count > 0)
                {
                    errors.AddRange(clusterErrors);
                    continue;
                }

                configuredClusters.Add(cluster.ClusterId, cluster);
            }
            catch (Exception ex)
            {
                errors.Add(new ArgumentException($"An exception was thrown from the configuration callbacks for cluster '{c.ClusterId}'.", ex));
            }
        }

        if (errors.Count > 0)
        {
            return (_emptyClusterDictionary, errors);
        }

        return (configuredClusters, errors);
    }

    private void UpdateRuntimeClusters(IReadOnlyDictionary<string, ClusterConfig> incomingClusters)
    {
        var desiredClusters = new HashSet<string>(incomingClusters.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var incomingCluster in incomingClusters.Values)
        {
            var added = desiredClusters.Add(incomingCluster.ClusterId);
            Debug.Assert(added);

            if (_clusters.TryGetValue(incomingCluster.ClusterId, out var currentCluster))
            {
                var destinationsChanged = UpdateRuntimeDestinations(incomingCluster.Destinations, currentCluster.Destinations);

                var currentClusterModel = currentCluster.Model;

                var httpClient = _httpClientFactory.CreateClient(new ForwarderHttpClientContext
                {
                    ClusterId = currentCluster.ClusterId,
                    OldConfig = currentClusterModel.Config.HttpClient ?? HttpClientConfig.Empty,
                    OldMetadata = currentClusterModel.Config.Metadata,
                    OldClient = currentClusterModel.HttpClient,
                    NewConfig = incomingCluster.HttpClient ?? HttpClientConfig.Empty,
                    NewMetadata = incomingCluster.Metadata
                });

                var newClusterModel = new ClusterModel(incomingCluster, httpClient);

                // Excludes destination changes, they're tracked separately.
                var configChanged = currentClusterModel.HasConfigChanged(newClusterModel);
                if (configChanged)
                {
                    currentCluster.Revision++;
                    Log.ClusterChanged(_logger, incomingCluster.ClusterId);
                }

                if (destinationsChanged || configChanged)
                {
                    // Config changed, so update runtime cluster
                    currentCluster.Model = newClusterModel;

                    _clusterDestinationsUpdater.UpdateAllDestinations(currentCluster);

                    foreach (var listener in _clusterChangeListeners)
                    {
                        listener.OnClusterChanged(currentCluster);
                    }
                }
            }
            else
            {
                var newClusterState = new ClusterState(incomingCluster.ClusterId);

                UpdateRuntimeDestinations(incomingCluster.Destinations, newClusterState.Destinations);

                var httpClient = _httpClientFactory.CreateClient(new ForwarderHttpClientContext
                {
                    ClusterId = newClusterState.ClusterId,
                    NewConfig = incomingCluster.HttpClient ?? HttpClientConfig.Empty,
                    NewMetadata = incomingCluster.Metadata
                });

                newClusterState.Model = new ClusterModel(incomingCluster, httpClient);
                newClusterState.Revision++;
                Log.ClusterAdded(_logger, incomingCluster.ClusterId);

                _clusterDestinationsUpdater.UpdateAllDestinations(newClusterState);

                added = _clusters.TryAdd(newClusterState.ClusterId, newClusterState);
                Debug.Assert(added);

                foreach (var listener in _clusterChangeListeners)
                {
                    listener.OnClusterAdded(newClusterState);
                }
            }
        }

        // Directly enumerate the ConcurrentDictionary to limit locking and copying.
        foreach (var existingClusterPair in _clusters)
        {
            var existingCluster = existingClusterPair.Value;
            if (!desiredClusters.Contains(existingCluster.ClusterId))
            {
                // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                //
                // NOTE 2: Removing the cluster from _clusters is safe and existing
                // ASP .NET Core endpoints will continue to work with their existing behavior (until those endpoints are updated)
                // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                Log.ClusterRemoved(_logger, existingCluster.ClusterId);
                var removed = _clusters.TryRemove(existingCluster.ClusterId, out var _);
                Debug.Assert(removed);

                foreach (var listener in _clusterChangeListeners)
                {
                    listener.OnClusterRemoved(existingCluster);
                }
            }
        }
    }

    private bool UpdateRuntimeDestinations(IReadOnlyDictionary<string, DestinationConfig>? incomingDestinations, ConcurrentDictionary<string, DestinationState> currentDestinations)
    {
        var desiredDestinations = new HashSet<string>(incomingDestinations?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        if (incomingDestinations is not null)
        {
            foreach (var incomingDestination in incomingDestinations)
            {
                var added = desiredDestinations.Add(incomingDestination.Key);
                Debug.Assert(added);

                if (currentDestinations.TryGetValue(incomingDestination.Key, out var currentDestination))
                {
                    if (currentDestination.Model.HasChanged(incomingDestination.Value))
                    {
                        Log.DestinationChanged(_logger, incomingDestination.Key);
                        currentDestination.Model = new DestinationModel(incomingDestination.Value);
                        changed = true;
                    }
                }
                else
                {
                    Log.DestinationAdded(_logger, incomingDestination.Key);
                    var newDestination = new DestinationState(incomingDestination.Key)
                    {
                        Model = new DestinationModel(incomingDestination.Value),
                    };
                    added = currentDestinations.TryAdd(newDestination.DestinationId, newDestination);
                    Debug.Assert(added);
                    changed = true;
                }
            }
        }

        // Directly enumerate the ConcurrentDictionary to limit locking and copying.
        foreach (var existingDestinationPair in currentDestinations)
        {
            var id = existingDestinationPair.Value.DestinationId;
            if (!desiredDestinations.Contains(id))
            {
                // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                //
                // NOTE 2: Removing the endpoint from `IEndpointManager` is safe and existing
                // clusters will continue to work with their existing behavior (until those clusters are updated)
                // and the Garbage Collector won't destroy this cluster object while it's referenced elsewhere.
                Log.DestinationRemoved(_logger, id);
                var removed = currentDestinations.TryRemove(id, out var _);
                Debug.Assert(removed);
                changed = true;
            }
        }

        return changed;
    }

    private bool UpdateRuntimeRoutes(IList<RouteConfig> incomingRoutes)
    {
        var desiredRoutes = new HashSet<string>(incomingRoutes.Count, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var incomingRoute in incomingRoutes)
        {
            var added = desiredRoutes.Add(incomingRoute.RouteId);
            Debug.Assert(added);

            // Note that this can be null, and that is fine. The resulting route may match
            // but would then fail to route, which is exactly what we were instructed to do in this case
            // since no valid cluster was specified.
            _clusters.TryGetValue(incomingRoute.ClusterId ?? string.Empty, out var cluster);

            if (_routes.TryGetValue(incomingRoute.RouteId, out var currentRoute))
            {
                if (currentRoute.Model.HasConfigChanged(incomingRoute, cluster, currentRoute.ClusterRevision))
                {
                    currentRoute.CachedEndpoint = null; // Recreate endpoint
                    var newModel = BuildRouteModel(incomingRoute, cluster);
                    currentRoute.Model = newModel;
                    currentRoute.ClusterRevision = cluster?.Revision;
                    changed = true;
                    Log.RouteChanged(_logger, currentRoute.RouteId);
                }
            }
            else
            {
                var newModel = BuildRouteModel(incomingRoute, cluster);
                var newState = new RouteState(incomingRoute.RouteId)
                {
                    Model = newModel,
                    ClusterRevision = cluster?.Revision,
                };
                added = _routes.TryAdd(newState.RouteId, newState);
                Debug.Assert(added);
                changed = true;
                Log.RouteAdded(_logger, newState.RouteId);
            }
        }

        // Directly enumerate the ConcurrentDictionary to limit locking and copying.
        foreach (var existingRoutePair in _routes)
        {
            var routeId = existingRoutePair.Value.RouteId;
            if (!desiredRoutes.Contains(routeId))
            {
                // NOTE 1: Remove is safe to do within the `foreach` loop on ConcurrentDictionary
                //
                // NOTE 2: Removing the route from _routes is safe and existing
                // ASP.NET Core endpoints will continue to work with their existing behavior since
                // their copy of `RouteModel` is immutable and remains operational in whichever state is was in.
                Log.RouteRemoved(_logger, routeId);
                var removed = _routes.TryRemove(routeId, out var _);
                Debug.Assert(removed);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Applies a new set of ASP .NET Core endpoints. Changes take effect immediately.
    /// </summary>
    /// <param name="endpoints">New endpoints to apply.</param>
    [MemberNotNull(nameof(_endpoints))]
    private void UpdateEndpoints(List<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        lock (_syncRoot)
        {
            // These steps are done in a specific order to ensure callers always see a consistent state.

            // Step 1 - capture old token
            var oldCancellationTokenSource = _endpointsChangeSource;

            // Step 2 - update endpoints
            Volatile.Write(ref _endpoints, endpoints);

            // Step 3 - create new change token
            _endpointsChangeSource = new CancellationTokenSource();
            Volatile.Write(ref _endpointsChangeToken, new CancellationChangeToken(_endpointsChangeSource.Token));

            // Step 4 - trigger old token
            oldCancellationTokenSource?.Cancel();
        }
    }

    private RouteModel BuildRouteModel(RouteConfig source, ClusterState? cluster)
    {
        var transforms = _transformBuilder.Build(source, cluster?.Model?.Config);

        return new RouteModel(source, cluster, transforms);
    }

    public bool TryGetRoute(string id, [NotNullWhen(true)] out RouteModel? route)
    {
        if (_routes.TryGetValue(id, out var routeState))
        {
            route = routeState.Model;
            return true;
        }

        route = null;
        return false;
    }

    public IEnumerable<RouteModel> GetRoutes()
    {
        foreach (var (_, route) in _routes)
        {
            yield return route.Model;
        }
    }

    public bool TryGetCluster(string id, [NotNullWhen(true)] out ClusterState? cluster)
    {
        return _clusters.TryGetValue(id, out cluster!);
    }

    public IEnumerable<ClusterState> GetClusters()
    {
        foreach (var (_, cluster) in _clusters)
        {
            yield return cluster;
        }
    }

    public void Dispose()
    {
        _configChangeSource.Dispose();
        foreach (var instance in _configs)
        {
            instance?.CallbackCleanup?.Dispose();
        }
    }

    private sealed class ConfigState
    {
        public ConfigState(IProxyConfigProvider provider, IProxyConfig config)
        {
            Provider = provider;
            LatestConfig = config;
        }

        public IProxyConfigProvider Provider { get; }

        public IProxyConfig LatestConfig { get; set; }

        public bool LoadFailed { get; set; }

        public IDisposable? CallbackCleanup { get; set; }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _clusterAdded = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.ClusterAdded,
            "Cluster '{clusterId}' has been added.");

        private static readonly Action<ILogger, string, Exception?> _clusterChanged = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.ClusterChanged,
            "Cluster '{clusterId}' has changed.");

        private static readonly Action<ILogger, string, Exception?> _clusterRemoved = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.ClusterRemoved,
            "Cluster '{clusterId}' has been removed.");

        private static readonly Action<ILogger, string, Exception?> _destinationAdded = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DestinationAdded,
            "Destination '{destinationId}' has been added.");

        private static readonly Action<ILogger, string, Exception?> _destinationChanged = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DestinationChanged,
            "Destination '{destinationId}' has changed.");

        private static readonly Action<ILogger, string, Exception?> _destinationRemoved = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.DestinationRemoved,
            "Destination '{destinationId}' has been removed.");

        private static readonly Action<ILogger, string, Exception?> _routeAdded = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.RouteAdded,
            "Route '{routeId}' has been added.");

        private static readonly Action<ILogger, string, Exception?> _routeChanged = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.RouteChanged,
            "Route '{routeId}' has changed.");

        private static readonly Action<ILogger, string, Exception?> _routeRemoved = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.RouteRemoved,
            "Route '{routeId}' has been removed.");

        private static readonly Action<ILogger, Exception> _errorReloadingConfig = LoggerMessage.Define(
            LogLevel.Error,
            EventIds.ErrorReloadingConfig,
            "Failed to reload config. Unable to register for change notifications, polling for changes until successful.");

        private static readonly Action<ILogger, Exception> _errorApplyingConfig = LoggerMessage.Define(
            LogLevel.Error,
            EventIds.ErrorApplyingConfig,
            "Failed to apply the new config.");

        public static void ClusterAdded(ILogger logger, string clusterId)
        {
            _clusterAdded(logger, clusterId, null);
        }

        public static void ClusterChanged(ILogger logger, string clusterId)
        {
            _clusterChanged(logger, clusterId, null);
        }

        public static void ClusterRemoved(ILogger logger, string clusterId)
        {
            _clusterRemoved(logger, clusterId, null);
        }

        public static void DestinationAdded(ILogger logger, string destinationId)
        {
            _destinationAdded(logger, destinationId, null);
        }

        public static void DestinationChanged(ILogger logger, string destinationId)
        {
            _destinationChanged(logger, destinationId, null);
        }

        public static void DestinationRemoved(ILogger logger, string destinationId)
        {
            _destinationRemoved(logger, destinationId, null);
        }

        public static void RouteAdded(ILogger logger, string routeId)
        {
            _routeAdded(logger, routeId, null);
        }

        public static void RouteChanged(ILogger logger, string routeId)
        {
            _routeChanged(logger, routeId, null);
        }

        public static void RouteRemoved(ILogger logger, string routeId)
        {
            _routeRemoved(logger, routeId, null);
        }

        public static void ErrorReloadingConfig(ILogger logger, Exception ex)
        {
            _errorReloadingConfig(logger, ex);
        }

        public static void ErrorApplyingConfig(ILogger logger, Exception ex)
        {
            _errorApplyingConfig(logger, ex);
        }
    }
}
