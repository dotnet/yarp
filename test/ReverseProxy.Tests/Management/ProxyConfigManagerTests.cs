// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Forwarder.Tests;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.ServiceDiscovery;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Management.Tests;

public class ProxyConfigManagerTests
{
    private static IServiceProvider CreateServices(
        List<RouteConfig> routes,
        List<ClusterConfig> clusters,
        Action<IReverseProxyBuilder> configureProxy = null,
        IEnumerable<IConfigChangeListener> configListeners = null,
        IDestinationResolver destinationResolver = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddRouting();
        var proxyBuilder = serviceCollection.AddReverseProxy().LoadFromMemory(routes, clusters);
        serviceCollection.TryAddSingleton(new Mock<IServer>().Object);
        serviceCollection.TryAddSingleton(new Mock<IWebHostEnvironment>().Object);
        var activeHealthPolicy = new Mock<IActiveHealthCheckPolicy>();
        activeHealthPolicy.SetupGet(p => p.Name).Returns("activePolicyA");
        serviceCollection.AddSingleton(activeHealthPolicy.Object);
        configureProxy?.Invoke(proxyBuilder);
        if (configListeners is not null)
        {
            foreach (var configListener in configListeners)
            {
                serviceCollection.AddSingleton(configListener);
            }
        }

        if (destinationResolver is not null)
        {
            serviceCollection.AddSingleton(destinationResolver);
        }

        var services = serviceCollection.BuildServiceProvider();
        var routeBuilder = services.GetRequiredService<ProxyEndpointFactory>();
        routeBuilder.SetProxyPipeline(context => Task.CompletedTask);
        return services;
    }

    private static IServiceProvider CreateServices(
        IEnumerable<IProxyConfigProvider> configProviders,
        Action<IReverseProxyBuilder> configureProxy = null,
        IEnumerable<IConfigChangeListener> configListeners = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddRouting();
        var proxyBuilder = serviceCollection.AddReverseProxy();
        foreach (var configProvider in configProviders)
        {
            serviceCollection.AddSingleton(configProvider);
        }
        serviceCollection.TryAddSingleton(new Mock<IServer>().Object);
        serviceCollection.TryAddSingleton(new Mock<IWebHostEnvironment>().Object);
        var activeHealthPolicy = new Mock<IActiveHealthCheckPolicy>();
        activeHealthPolicy.SetupGet(p => p.Name).Returns("activePolicyA");
        serviceCollection.AddSingleton(activeHealthPolicy.Object);
        configureProxy?.Invoke(proxyBuilder);
        if (configListeners is not null)
        {
            foreach (var configListener in configListeners)
            {
                serviceCollection.AddSingleton(configListener);
            }
        }
        var services = serviceCollection.BuildServiceProvider();
        var routeBuilder = services.GetRequiredService<ProxyEndpointFactory>();
        routeBuilder.SetProxyPipeline(context => Task.CompletedTask);
        return services;
    }

    [Fact]
    public void Constructor_Works()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        _ = services.GetRequiredService<ProxyConfigManager>();
    }

    [Fact]
    public async Task NullRoutes_StartsEmpty()
    {
        var services = CreateServices(null, new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task NullClusters_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), null);
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task Endpoints_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Empty(endpoints);
    }

    [Fact]
    public async Task Lookup_StartsEmpty()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var lookup = services.GetRequiredService<IProxyStateLookup>();
        await manager.InitialLoadAsync();

        Assert.Empty(lookup.GetRoutes());
        Assert.Empty(lookup.GetClusters());
        Assert.False(lookup.TryGetRoute("route1", out var _));
        Assert.False(lookup.TryGetCluster("cluster1", out var _));
    }

    [Fact]
    public async Task GetChangeToken_InitialValue()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();
        Assert.NotNull(dataSource);
        var changeToken = dataSource.GetChangeToken();
        Assert.NotNull(changeToken);
        Assert.True(changeToken.ActiveChangeCallbacks);
        Assert.False(changeToken.HasChanged);
    }

    [Fact]
    public async Task BuildConfig_OneClusterOneDestinationOneRoute_Works()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig>() { route }, new List<ClusterConfig>() { cluster });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var lookup = services.GetRequiredService<IProxyStateLookup>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        var endpoint = Assert.Single(endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);
        Assert.True(lookup.TryGetRoute("route1", out var routeModel));
        Assert.Equal(route, routeModel.Config);
        routeModel = Assert.Single(lookup.GetRoutes());
        Assert.Equal(route, routeModel.Config);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);
        Assert.True(lookup.TryGetCluster("cluster1", out clusterState));
        Assert.Equal(cluster, clusterState.Model.Config);
        clusterState = Assert.Single(lookup.GetClusters());
        Assert.Equal(cluster, clusterState.Model.Config);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    [Fact]
    public async Task BuildConfig_DuplicateRouteIds_Throws()
    {
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig> { route, route }, new List<ClusterConfig>());

        var manager = services.GetRequiredService<ProxyConfigManager>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InitialLoadAsync());
        Assert.Contains("Duplicate route 'route1'", ex.ToString());
    }

    [Fact]
    public async Task BuildConfig_DuplicateClusterIds_Throws()
    {
        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1"
        };
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig> { route }, new List<ClusterConfig> { cluster, cluster });

        var manager = services.GetRequiredService<ProxyConfigManager>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InitialLoadAsync());
        Assert.Contains("Duplicate cluster 'cluster1'", ex.ToString());
    }

    [Fact]
    public async Task BuildConfig_TwoDistinctConfigs_Works()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };

        var config1 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster1 });
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster2 });

        var services = CreateServices(new[] { config1, config2 });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Equal(2, endpoints.Count);

        // The order is unstable because routes are stored in a dictionary.
        var routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route1")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);

        routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route2")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route2", routeConfig.Config.RouteId);

        clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster2", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        actualDestinations = clusterState.Destinations.Values;
        destination = Assert.Single(actualDestinations);
        Assert.Equal("d2", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    [Fact]
    public async Task BuildConfig_TwoOverlappingConfigs_Works()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };

        var config1 = new InMemoryConfigProvider(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster1 });
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster2 });

        var services = CreateServices(new[] { config1, config2 });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoints = dataSource.Endpoints;
        Assert.Equal(2, endpoints.Count);

        // The order is unstable because routes are stored in a dictionary.
        var routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route1")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route1", routeConfig.Config.RouteId);

        var clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster1", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        var actualDestinations = clusterState.Destinations.Values;
        var destination = Assert.Single(actualDestinations);
        Assert.Equal("d1", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);

        routeConfig = endpoints.Single(e => string.Equals(e.DisplayName, "route2")).Metadata.GetMetadata<RouteModel>();
        Assert.NotNull(routeConfig);
        Assert.Equal("route2", routeConfig.Config.RouteId);

        clusterState = routeConfig.Cluster;
        Assert.NotNull(clusterState);

        Assert.Equal("cluster2", clusterState.ClusterId);
        Assert.NotNull(clusterState.Destinations);
        Assert.NotNull(clusterState.Model);
        Assert.NotNull(clusterState.Model.HttpClient);
        Assert.Same(clusterState, routeConfig.Cluster);

        actualDestinations = clusterState.Destinations.Values;
        destination = Assert.Single(actualDestinations);
        Assert.Equal("d2", destination.DestinationId);
        Assert.NotNull(destination.Model);
        Assert.Equal(TestAddress, destination.Model.Config.Address);
    }

    private class FakeConfigChangeListener : IConfigChangeListener
    {
        public bool? HasApplyingSucceeded { get; private set; }
        public bool DidAtLeastOneErrorOccurWhileLoading { get; private set; }
        public string[] EventuallyLoaded;
        public string[] SuccessfullyApplied;
        public string[] FailedApplied;

        public FakeConfigChangeListener()
        {
            Reset();
        }

        public void Reset()
        {
            DidAtLeastOneErrorOccurWhileLoading = false;
            HasApplyingSucceeded = null;
            EventuallyLoaded = Array.Empty<string>();
            SuccessfullyApplied = Array.Empty<string>();
            FailedApplied = Array.Empty<string>();
        }

        public void ConfigurationLoadingFailed(IProxyConfigProvider configProvider, Exception ex)
        {
            DidAtLeastOneErrorOccurWhileLoading = true;
        }

        public void ConfigurationLoaded(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            EventuallyLoaded = proxyConfigs.Select(c => c.RevisionId).ToArray();
        }

        public void ConfigurationApplyingFailed(IReadOnlyList<IProxyConfig> proxyConfigs, Exception ex)
        {
            HasApplyingSucceeded = false;
            FailedApplied = proxyConfigs.Select(c => c.RevisionId).ToArray();
        }

        public void ConfigurationApplied(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            HasApplyingSucceeded = true;
            SuccessfullyApplied = proxyConfigs.Select(c => c.RevisionId).ToArray();
        }
    }

    private class ConfigChangeListenerCounter : IConfigChangeListener
    {
        public int NumberOfLoadedConfigurations { get; private set; }
        public int NumberOfFailedConfigurationLoads { get; private set; }
        public int NumberOfAppliedConfigurations { get; private set; }
        public int NumberOfFailedConfigurationApplications { get; private set; }

        public ConfigChangeListenerCounter()
        {
            Reset();
        }

        public void Reset()
        {
            NumberOfLoadedConfigurations = 0;
            NumberOfFailedConfigurationLoads = 0;
            NumberOfAppliedConfigurations = 0;
            NumberOfFailedConfigurationApplications = 0;
        }

        public void ConfigurationLoadingFailed(IProxyConfigProvider configProvider, Exception ex)
        {
            NumberOfFailedConfigurationLoads++;
        }

        public void ConfigurationLoaded(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            NumberOfLoadedConfigurations += proxyConfigs.Count;
        }

        public void ConfigurationApplyingFailed(IReadOnlyList<IProxyConfig> proxyConfigs, Exception ex)
        {
            NumberOfFailedConfigurationApplications += proxyConfigs.Count;
        }

        public void ConfigurationApplied(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            NumberOfAppliedConfigurations += proxyConfigs.Count;
        }
    }

    private class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, string revisionId)
        {
            RevisionId = revisionId;
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public string RevisionId { get; }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }

    private class OnDemandFailingInMemoryConfigProvider : IProxyConfigProvider
    {
        private volatile InMemoryConfig _config;

        public OnDemandFailingInMemoryConfigProvider(
            InMemoryConfig config)
        {
            _config = config;
        }

        public OnDemandFailingInMemoryConfigProvider(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            string revisionId) : this(new InMemoryConfig(routes, clusters, revisionId))
        {
        }

        public IProxyConfig GetConfig()
        {
            if (ShouldConfigLoadingFail)
            {
                return null;
            }

            return _config;
        }

        public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, string revisionId)
        {
            Update(new InMemoryConfig(routes, clusters, revisionId));
        }

        public void Update(InMemoryConfig config)
        {
            var oldConfig = Interlocked.Exchange(ref _config, config);
            oldConfig.SignalChange();
        }

        public bool ShouldConfigLoadingFail { get; set; }
    }

    [Fact]
    public async Task BuildConfig_CanBeNotifiedOfProxyConfigSuccessfulAndFailedLoading()
    {
        var configProviderA = new OnDemandFailingInMemoryConfigProvider(new List<RouteConfig>() { }, new List<ClusterConfig>() { }, "A1");
        var configProviderB = new OnDemandFailingInMemoryConfigProvider(new List<RouteConfig>() { }, new List<ClusterConfig>() { }, "B1");

        var configChangeListenerCounter = new ConfigChangeListenerCounter();
        var fakeConfigChangeListener = new FakeConfigChangeListener();

        var services = CreateServices(new[] { configProviderA, configProviderB }, null, new IConfigChangeListener[] { fakeConfigChangeListener, configChangeListenerCounter });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        await manager.InitialLoadAsync();

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(2, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.False(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A1", "B1" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.True(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Equal(new[] { "A1", "B1" }, fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Empty(fakeConfigChangeListener.FailedApplied);

        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };

        fakeConfigChangeListener.Reset();
        configChangeListenerCounter.Reset();

        configProviderA.Update(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster1 }, "A2");

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(2, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.False(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.True(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Empty(fakeConfigChangeListener.FailedApplied);

        configProviderB.ShouldConfigLoadingFail = true;

        fakeConfigChangeListener.Reset();
        configChangeListenerCounter.Reset();

        configProviderB.Update(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster2 }, "B2");

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(1, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(2, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.True(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.True(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Empty(fakeConfigChangeListener.FailedApplied);
    }

    [Fact]
    public async Task BuildConfig_CanBeNotifiedOfProxyConfigSuccessfulAndFailedUpdating()
    {
        var configProviderA = new InMemoryConfigProvider(new List<RouteConfig>() { }, new List<ClusterConfig>() { }, "A1");
        var configProviderB = new InMemoryConfigProvider(new List<RouteConfig>() { }, new List<ClusterConfig>() { }, "B1");

        var configChangeListenerCounter = new ConfigChangeListenerCounter();
        var fakeConfigChangeListener = new FakeConfigChangeListener();

        var services = CreateServices(new[] { configProviderA, configProviderB }, null, new IConfigChangeListener[] { fakeConfigChangeListener, configChangeListenerCounter });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        await manager.InitialLoadAsync();

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(2, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.False(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A1", "B1" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.True(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Equal(new[] { "A1", "B1" }, fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Empty(fakeConfigChangeListener.FailedApplied);

        const string TestAddress = "https://localhost:123/";

        var cluster1 = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            }
        };
        var cluster2 = new ClusterConfig
        {
            ClusterId = "cluster2",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d2", new DestinationConfig { Address = TestAddress } }
            }
        };

        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            // Missing Match here will be caught by the analysis
        };

        fakeConfigChangeListener.Reset();
        configChangeListenerCounter.Reset();

        configProviderA.Update(new List<RouteConfig>() { route1 }, new List<ClusterConfig>() { cluster1 }, "A2");

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(2, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.False(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.True(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Equal(new[] { "A2", "B1" }, fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Empty(fakeConfigChangeListener.FailedApplied);

        fakeConfigChangeListener.Reset();
        configChangeListenerCounter.Reset();

        configProviderB.Update(new List<RouteConfig>() { route2 }, new List<ClusterConfig>() { cluster2 }, "B2");

        Assert.Equal(2, configChangeListenerCounter.NumberOfLoadedConfigurations);
        Assert.Equal(0, configChangeListenerCounter.NumberOfFailedConfigurationLoads);
        Assert.Equal(0, configChangeListenerCounter.NumberOfAppliedConfigurations);
        Assert.Equal(2, configChangeListenerCounter.NumberOfFailedConfigurationApplications);

        Assert.False(fakeConfigChangeListener.DidAtLeastOneErrorOccurWhileLoading);
        Assert.Equal(new[] { "A2", "B2" }, fakeConfigChangeListener.EventuallyLoaded);
        Assert.False(fakeConfigChangeListener.HasApplyingSucceeded);
        Assert.Empty(fakeConfigChangeListener.SuccessfullyApplied);
        Assert.Equal(new[] { "A2", "B2" }, fakeConfigChangeListener.FailedApplied);
    }

    public class DummyProxyConfig : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes => throw new NotImplementedException();
        public IReadOnlyList<ClusterConfig> Clusters => throw new NotImplementedException();
        public IChangeToken ChangeToken => throw new NotImplementedException();
    }

    [Fact]
    public void IProxyConfigDerivedTypes_RevisionIdIsAutomaticallySet()
    {
        IProxyConfig config = new DummyProxyConfig();
        Assert.NotNull(config.RevisionId);
        Assert.NotEmpty(config.RevisionId);
        Assert.Same(config.RevisionId, config.RevisionId);
    }

    [Fact]
    public async Task InitialLoadAsync_ProxyHttpClientOptionsSet_CreateAndSetHttpClient()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            },
            HttpClient = new HttpClientConfig
            {
                SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                MaxConnectionsPerServer = 10,
                RequestHeaderEncoding = Encoding.UTF8.WebName,
                ResponseHeaderEncoding = Encoding.UTF8.WebName,
            },
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig { Enabled = true }
            }
        };
        var route = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };

        var services = CreateServices(new List<RouteConfig>() { route }, new List<ClusterConfig>() { cluster });

        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        var endpoint = Assert.Single(dataSource.Endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        var clusterState = routeConfig.Cluster;
        Assert.Equal("cluster1", clusterState.ClusterId);
        var clusterModel = clusterState.Model;
        Assert.NotNull(clusterModel.HttpClient);
        Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, clusterModel.Config.HttpClient.SslProtocols);
        Assert.Equal(10, clusterModel.Config.HttpClient.MaxConnectionsPerServer);
        Assert.Equal(Encoding.UTF8.WebName, clusterModel.Config.HttpClient.RequestHeaderEncoding);
        Assert.Equal(Encoding.UTF8.WebName, clusterModel.Config.HttpClient.ResponseHeaderEncoding);

        var handler = ForwarderHttpClientFactoryTests.GetHandler(clusterModel.HttpClient);
        Assert.Equal(SslProtocols.Tls11 | SslProtocols.Tls12, handler.SslOptions.EnabledSslProtocols);
        Assert.Equal(10, handler.MaxConnectionsPerServer);
        Assert.Equal(Encoding.UTF8, handler.RequestHeaderEncodingSelector(default, default));
        var activeMonitor = (ActiveHealthCheckMonitor)services.GetRequiredService<IActiveHealthCheckMonitor>();
        Assert.True(activeMonitor.Scheduler.IsScheduled(clusterState));
    }

    [Fact]
    public async Task GetChangeToken_SignalsChange()
    {
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>());
        var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();
        _ = configManager.Endpoints; // Lazily creates endpoints the first time, activates change notifications.

        var signaled1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var signaled2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<Endpoint> readEndpoints1 = null;
        IReadOnlyList<Endpoint> readEndpoints2 = null;

        var changeToken1 = dataSource.GetChangeToken();
        changeToken1.RegisterChangeCallback(
            _ =>
            {
                readEndpoints1 = dataSource.Endpoints;
                signaled1.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled1.Task.IsCompleted);
        inMemoryConfig.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled1.Task.DefaultTimeout();

        var changeToken2 = dataSource.GetChangeToken();
        changeToken2.RegisterChangeCallback(
            _ =>
            {
                readEndpoints2 = dataSource.Endpoints;
                signaled2.SetResult(1);
            }, null);

        // updating again should only signal the new change token
        Assert.False(signaled2.Task.IsCompleted);
        inMemoryConfig.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r2", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled2.Task.DefaultTimeout();

        Assert.NotNull(readEndpoints1);
        Assert.NotNull(readEndpoints2);
    }

    [Fact]
    public async Task GetChangeToken_MultipleConfigs_SignalsChange()
    {
        var config1 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>());
        var config2 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>());
        var services = CreateServices(new[] { config1, config2 });
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();
        _ = configManager.Endpoints; // Lazily creates endpoints the first time, activates change notifications.

        var signaled1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var signaled2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<Endpoint> readEndpoints1 = null;
        IReadOnlyList<Endpoint> readEndpoints2 = null;

        var changeToken1 = dataSource.GetChangeToken();
        changeToken1.RegisterChangeCallback(
            _ =>
            {
                readEndpoints1 = dataSource.Endpoints;
                signaled1.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled1.Task.IsCompleted);
        config1.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled1.Task.DefaultTimeout();

        var changeToken2 = dataSource.GetChangeToken();
        changeToken2.RegisterChangeCallback(
            _ =>
            {
                readEndpoints2 = dataSource.Endpoints;
                signaled2.SetResult(1);
            }, null);

        // updating again should only signal the new change token
        Assert.False(signaled2.Task.IsCompleted);
        config2.Update(new List<RouteConfig>() { new RouteConfig() { RouteId = "r2", Match = new RouteMatch { Path = "/" } } }, new List<ClusterConfig>());
        await signaled2.Task.DefaultTimeout();

        var endpoint = Assert.Single(readEndpoints1);
        Assert.Equal("r1", endpoint.DisplayName);

        Assert.NotNull(readEndpoints2);
        Assert.Equal(2, readEndpoints2.Count);
        // Ordering is unstable due to dictionary storage.
        readEndpoints2.Single(e => string.Equals(e.DisplayName, "r1"));
        readEndpoints2.Single(e => string.Equals(e.DisplayName, "r2"));
    }

    [Fact]
    public async Task ChangeConfig_ActiveHealthCheckIsEnabled_RunInitialCheck()
    {
        var endpoints = new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", ClusterId = "c1", Match = new RouteMatch { Path = "/" } } };
        var clusters = new List<ClusterConfig>() { new ClusterConfig { ClusterId = "c1" } };
        var services = CreateServices(endpoints, clusters);
        var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();

        var endpoint = Assert.Single(dataSource.Endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        var clusterState = routeConfig.Cluster;
        var activeMonitor = (ActiveHealthCheckMonitor)services.GetRequiredService<IActiveHealthCheckMonitor>();
        Assert.False(activeMonitor.Scheduler.IsScheduled(clusterState));

        var signaled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var changeToken = dataSource.GetChangeToken();
        changeToken.RegisterChangeCallback(
            _ =>
            {
                signaled.SetResult(1);
            }, null);

        // updating should signal the current change token
        Assert.False(signaled.Task.IsCompleted);
        inMemoryConfig.Update(
            endpoints,
            new List<ClusterConfig>()
            {
                new ClusterConfig
                {
                    ClusterId = "c1",
                    HealthCheck = new HealthCheckConfig { Active = new ActiveHealthCheckConfig { Enabled = true } }
                }
            });
        await signaled.Task.DefaultTimeout();

        Assert.True(activeMonitor.Scheduler.IsScheduled(clusterState));
    }

    [Fact]
    public async Task ChangeConfig_DestinationChange_IsReflectedOnRouteConfiguration()
    {
        var endpoints = new List<RouteConfig>() { new RouteConfig() { RouteId = "r1", ClusterId = "c1", Match = new RouteMatch { Path = "/" } } };
        var clusters = new List<ClusterConfig>()
        {
            new ClusterConfig
            {
                ClusterId = "c1",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { "d1", new DestinationConfig() { Address = "http://d1" } }
                }
            }
        };
        var services = CreateServices(endpoints, clusters);
        var inMemoryConfig = (InMemoryConfigProvider)services.GetRequiredService<IProxyConfigProvider>();
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await configManager.InitialLoadAsync();

        var endpoint = Assert.Single(dataSource.Endpoints);
        var routeConfig = endpoint.Metadata.GetMetadata<RouteModel>();
        Assert.Equal("http://d1", Assert.Single(routeConfig.Cluster.Destinations).Value.Model.Config.Address);
        Assert.Equal(1, routeConfig.Cluster.Revision);

        inMemoryConfig.Update(
            endpoints,
            new List<ClusterConfig>()
            {
                new ClusterConfig
                {
                    ClusterId = "c1",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "d1", new DestinationConfig() { Address = "http://d1-v2" } }
                    }
                }
            });

        var destinationConfig = Assert.Single(routeConfig.Cluster.Destinations).Value.Model.Config;
        Assert.Equal("http://d1-v2", destinationConfig.Address);

        Assert.Same(destinationConfig, Assert.Single(routeConfig.Cluster.DestinationsState.AllDestinations).Model.Config);
        Assert.Same(destinationConfig, Assert.Single(routeConfig.Cluster.Model.Config.Destinations).Value);

        // Destination changes do not affect this property
        Assert.Equal(1, routeConfig.Cluster.Revision);
    }

    [Fact]
    public async Task LoadAsync_RequestVersionValidationError_Throws()
    {
        const string TestAddress = "https://localhost:123/";

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig { Address = TestAddress } }
            },
            HttpRequest = new ForwarderRequestConfig() { Version = new Version(1, 2) }
        };

        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Single(agex.InnerExceptions);
        var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
        Assert.StartsWith("Outgoing request version", argex.Message);
    }

    [Fact]
    public async Task LoadAsync_RouteValidationError_Throws()
    {
        var routeName = "route1";
        var route1 = new RouteConfig { RouteId = routeName, Match = new RouteMatch { Hosts = null }, ClusterId = "cluster1" };
        var services = CreateServices(new List<RouteConfig>() { route1 }, new List<ClusterConfig>());
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Single(agex.InnerExceptions);
        var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
        Assert.StartsWith($"Route '{routeName}' requires Hosts or Path specified", argex.Message);
    }

    [Fact]
    public async Task LoadAsync_MultipleSourcesWithValidationErrors_Throws()
    {
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = null }, ClusterId = "cluster1" };
        var provider1 = new InMemoryConfigProvider(new List<RouteConfig>() { route1 }, new List<ClusterConfig>());
        var cluster1 = new ClusterConfig { ClusterId = "cluster1", HttpClient = new HttpClientConfig { MaxConnectionsPerServer = -1 } };
        var provider2 = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>() { cluster1 });
        var services = CreateServices(new[] { provider1, provider2 });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Equal(2, agex.InnerExceptions.Count);
        var argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.First());
        Assert.StartsWith($"Route 'route1' requires Hosts or Path specified", argex.Message);
        argex = Assert.IsType<ArgumentException>(agex.InnerExceptions.Skip(1).First());
        Assert.StartsWith($"Max connections per server limit set on the cluster 'cluster1' must be positive.", argex.Message);
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterRouteActions_CanFixBrokenRoute()
    {
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = null }, Order = 1, ClusterId = "cluster1" };
        var services = CreateServices(new List<RouteConfig>() { route1 }, new List<ClusterConfig>(), proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<FixRouteHostFilter>();
        });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var dataSource = await configManager.InitialLoadAsync();
        var endpoints = dataSource.Endpoints;

        Assert.Single(endpoints);
        var endpoint = endpoints.Single();
        Assert.Same(route1.RouteId, endpoint.DisplayName);
        var hostMetadata = endpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.NotNull(hostMetadata);
        var host = Assert.Single(hostMetadata.Hosts);
        Assert.Equal("example.com", host);
    }

    private class FixRouteHostFilter : IProxyConfigFilter
    {
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<ClusterConfig>(cluster);
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<RouteConfig>(route with
            {
                Match = route.Match with { Hosts = new[] { "example.com" } }
            });
        }
    }

    private class ClusterAndRouteFilter : IProxyConfigFilter
    {
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            return new ValueTask<ClusterConfig>(cluster with
            {
                HealthCheck = new HealthCheckConfig()
                {
                    Active = new ActiveHealthCheckConfig { Enabled = true, Interval = TimeSpan.FromSeconds(12), Policy = "activePolicyA" }
                }
            });
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            string order;
            if (cluster is not null)
            {
                order = cluster.Metadata["Order"];
            }
            else
            {
                order = "12";
            }

            return new ValueTask<RouteConfig>(route with { Order = int.Parse(order) });
        }
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterConfiguresCluster_Works()
    {
        var route1 = new RouteConfig
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/" }
        };
        var route2 = new RouteConfig
        {
            RouteId = "route2",
            ClusterId = "cluster2",
            Match = new RouteMatch { Path = "/" }
        };
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            },
            Metadata = new Dictionary<string, string>
            {
                ["Order"] = "47"
            }
        };
        var services = CreateServices(new List<RouteConfig>() { route1, route2 }, new List<ClusterConfig>() { cluster }, proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteFilter>();
        });
        var manager = services.GetRequiredService<ProxyConfigManager>();
        var dataSource = await manager.InitialLoadAsync();

        Assert.NotNull(dataSource);
        Assert.Equal(2, dataSource.Endpoints.Count);

        var endpoint1 = Assert.Single(dataSource.Endpoints, e => e.DisplayName == "route1");
        var routeConfig1 = endpoint1.Metadata.GetMetadata<RouteModel>();
        Assert.Equal(47, routeConfig1.Config.Order);
        var clusterState1 = routeConfig1.Cluster;
        Assert.NotNull(clusterState1);
        Assert.True(clusterState1.Model.Config.HealthCheck.Active.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(12), clusterState1.Model.Config.HealthCheck.Active.Interval);
        var destination = Assert.Single(clusterState1.DestinationsState.AllDestinations);
        Assert.Equal("http://localhost", destination.Model.Config.Address);

        var endpoint2 = Assert.Single(dataSource.Endpoints, e => e.DisplayName == "route2");
        var routeConfig2 = endpoint2.Metadata.GetMetadata<RouteModel>();
        Assert.Equal(12, routeConfig2.Config.Order);
    }

    private class ClusterAndRouteThrows : IProxyConfigFilter
    {
        public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        {
            throw new NotFiniteNumberException("Test exception");
        }

        public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig cluster, CancellationToken cancel)
        {
            throw new NotFiniteNumberException("Test exception");
        }
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterClusterActionThrows_Throws()
    {
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster }, proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
        });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Single(agex.InnerExceptions);
        Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.First().InnerException);
    }

    [Fact]
    public async Task LoadAsync_ConfigFilterRouteActionThrows_Throws()
    {
        var route1 = new RouteConfig { RouteId = "route1", Match = new RouteMatch { Hosts = new[] { "example.com" } }, Order = 1, ClusterId = "cluster1" };
        var route2 = new RouteConfig { RouteId = "route2", Match = new RouteMatch { Hosts = new[] { "example2.com" } }, Order = 1, ClusterId = "cluster2" };
        var services = CreateServices(new List<RouteConfig>() { route1, route2 }, new List<ClusterConfig>(), proxyBuilder =>
        {
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
            proxyBuilder.AddConfigFilter<ClusterAndRouteThrows>();
        });
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);
        var agex = Assert.IsType<AggregateException>(ioEx.InnerException);

        Assert.Equal(2, agex.InnerExceptions.Count);
        Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.First().InnerException);
        Assert.IsType<NotFiniteNumberException>(agex.InnerExceptions.Skip(1).First().InnerException);
    }

    private class FakeDestinationResolver : IDestinationResolver
    {
        private readonly Func<IReadOnlyDictionary<string, DestinationConfig>, CancellationToken, ValueTask<ResolvedDestinationCollection>> _delegate;

        public FakeDestinationResolver(
            Func<IReadOnlyDictionary<string, DestinationConfig>, CancellationToken, ValueTask<ResolvedDestinationCollection>> @delegate)
        {
            _delegate = @delegate;
        }

        public ValueTask<ResolvedDestinationCollection> ResolveDestinationsAsync(IReadOnlyDictionary<string, DestinationConfig> destinations, CancellationToken cancellationToken)
            => _delegate(destinations, cancellationToken);
    }

    private class TestConfigChangeListener : IConfigChangeListener
    {
        private readonly bool _includeLoad;
        private readonly bool _includeApply;

        public Channel<ConfigChangeListenerEvent> Events { get; } = Channel.CreateUnbounded<ConfigChangeListenerEvent>();

        public TestConfigChangeListener(bool includeLoad = true, bool includeApply = true)
        {
            _includeLoad = includeLoad;
            _includeApply = includeApply;
        }

        public void ConfigurationApplied(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            if (!_includeApply)
            {
                return;
            }

            Assert.True(Events.Writer.TryWrite(new ConfigurationAppliedEvent(proxyConfigs)));
        }

        public void ConfigurationApplyingFailed(IReadOnlyList<IProxyConfig> proxyConfigs, Exception exception)
        {
            if (!_includeApply)
            {
                return;
            }

            Assert.True(Events.Writer.TryWrite(new ConfigurationApplyingFailedEvent(proxyConfigs, exception)));
        }

        public void ConfigurationLoaded(IReadOnlyList<IProxyConfig> proxyConfigs)
        {
            if (!_includeLoad)
            {
                return;
            }

            Assert.True(Events.Writer.TryWrite(new ConfigurationLoadedEvent(proxyConfigs)));
        }

        public void ConfigurationLoadingFailed(IProxyConfigProvider configProvider, Exception exception)
        {
            if (!_includeLoad)
            {
                return;
            }

            Assert.True(Events.Writer.TryWrite(new ConfigurationLoadingFailedEvent(configProvider, exception)));
        }

        public record ConfigChangeListenerEvent { };
        public record ConfigurationAppliedEvent(IReadOnlyList<IProxyConfig> ProxyConfigs) : ConfigChangeListenerEvent;
        public record ConfigurationApplyingFailedEvent(IReadOnlyList<IProxyConfig> ProxyConfigs, Exception exception) : ConfigChangeListenerEvent;
        public record ConfigurationLoadedEvent(IReadOnlyList<IProxyConfig> ProxyConfigs) : ConfigChangeListenerEvent;
        public record ConfigurationLoadingFailedEvent(IProxyConfigProvider ConfigProvider, Exception Exception) : ConfigChangeListenerEvent;
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Initial_ThrowsSync()
    {
        var throwResolver = new FakeDestinationResolver((destinations, cancellation) => throw new InvalidOperationException("Throwing!"));

        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(
            new List<RouteConfig>(),
            new List<ClusterConfig>() { cluster },
            destinationResolver: throwResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);

        var innerExc = Assert.IsType<InvalidOperationException>(ioEx.InnerException);
        Assert.Equal("Throwing!", innerExc.Message);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Initial_ThrowsAsync()
    {
        var throwResolver = new FakeDestinationResolver((destinations, cancellation) => ValueTask.FromException<ResolvedDestinationCollection>(new InvalidOperationException("Throwing!")));

        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster }, destinationResolver: throwResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        var ioEx = await Assert.ThrowsAsync<InvalidOperationException>(() => configManager.InitialLoadAsync());
        Assert.Equal("Unable to load or apply the proxy configuration.", ioEx.Message);

        var innerExc1 = Assert.IsType<InvalidOperationException>(ioEx.InnerException);
        Assert.Equal("Error resolving destinations for cluster cluster1", innerExc1.Message);
        var innerExc2 = Assert.IsType<InvalidOperationException>(innerExc1.InnerException);
        Assert.Equal("Throwing!", innerExc2.Message);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Successful()
    {
        var destinationsToExpand = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1", new DestinationConfig() { Address = "http://localhost" } }
        };

        var syncExpandResolver = new FakeDestinationResolver((destinations, cancellation) =>
        {
            var expandedDestinations = new Dictionary<string, DestinationConfig>();

            foreach (var destKvp in destinations)
            {
                expandedDestinations[$"{destKvp.Key}-1"] = new DestinationConfig { Address = "http://127.0.0.1:8080" };
                expandedDestinations[$"{destKvp.Key}-2"] = new DestinationConfig { Address = "http://127.1.1.1:8080" };
            }

            var result = new ResolvedDestinationCollection(expandedDestinations, null);
            return new(result);
        });

        var cluster1 = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = destinationsToExpand
        };

        var services = CreateServices(new List<RouteConfig>(), new List<ClusterConfig>() { cluster1 }, destinationResolver: syncExpandResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        await configManager.InitialLoadAsync();

        Assert.True(configManager.TryGetCluster(cluster1.ClusterId, out var cluster));

        var expectedDestinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1-1", new DestinationConfig() { Address = "http://127.0.0.1:8080" } },
            { "d1-2", new DestinationConfig() { Address = "http://127.1.1.1:8080" } }
        };

        var actualDestinations = cluster.Destinations.ToDictionary(static k => k.Key, static v => v.Value.Model.Config);
        Assert.Equal(expectedDestinations, actualDestinations);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Dns()
    {
        var destinationsToExpand = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1", new DestinationConfig() { Address = "http://localhost/a/b/c", Health = "http://localhost/healthz" } },
            { "d2", new DestinationConfig() { Address = "http://localhost:8080/a/b/c", Health = "http://localhost:8080/healthz"} },
            { "d3", new DestinationConfig() { Address = "https://localhost/a/b/c", Health = "https://localhost/healthz" } },
            { "d4", new DestinationConfig() { Address = "https://localhost:8443/a/b/c", Health = "https://localhost:8443/healthz", Host = "overriddenhost" } }
        };

        var cluster1 = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = destinationsToExpand
        };

        var services = CreateServices(
            new List<RouteConfig>(),
            new List<ClusterConfig>() { cluster1 },
            configureProxy: rp => rp.AddDnsDestinationResolver(o => o.AddressFamily = AddressFamily.InterNetwork));
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        await configManager.InitialLoadAsync();

        Assert.True(configManager.TryGetCluster(cluster1.ClusterId, out var cluster));

        var expectedDestinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1[127.0.0.1]", new DestinationConfig() { Address = "http://127.0.0.1/a/b/c", Health = "http://127.0.0.1/healthz", Host = "localhost" } },
            { "d2[127.0.0.1]", new DestinationConfig() { Address = "http://127.0.0.1:8080/a/b/c", Health = "http://127.0.0.1:8080/healthz", Host = "localhost:8080" } },
            { "d3[127.0.0.1]", new DestinationConfig() { Address = "https://127.0.0.1/a/b/c", Health = "https://127.0.0.1/healthz", Host = "localhost" } },
            { "d4[127.0.0.1]", new DestinationConfig() { Address = "https://127.0.0.1:8443/a/b/c", Health = "https://127.0.0.1:8443/healthz", Host = "overriddenhost" } }
        };

        var actualDestinations = cluster.Destinations.ToDictionary(static k => k.Key, static v => v.Value.Model.Config);
        Assert.Equal(expectedDestinations, actualDestinations);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_ReloadResolution()
    {
        var configListener = new TestConfigChangeListener(includeApply: false);
        var destinationsToExpand = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1", new DestinationConfig() { Address = "http://localhost" } }
        };

        var cts = new[] { new CancellationTokenSource() };
        var signaled = new[] { 0 };
        var syncExpandResolver = new FakeDestinationResolver((destinations, cancellation) =>
        {
            signaled[0]++;
            var expandedDestinations = new Dictionary<string, DestinationConfig>();

            foreach (var destKvp in destinations)
            {
                expandedDestinations[$"{destKvp.Key}-1"] = new DestinationConfig { Address = $"http://127.0.0.1:8080/{signaled[0]}" };
                expandedDestinations[$"{destKvp.Key}-2"] = new DestinationConfig { Address = $"http://127.1.1.1:8080/{signaled[0]}" };
            }

            var result = new ResolvedDestinationCollection(expandedDestinations, new CancellationChangeToken(cts[0].Token));
            return new(result);
        });

        var cluster1 = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = destinationsToExpand
        };

        var services = CreateServices(
            new List<RouteConfig>(),
            new List<ClusterConfig>() { cluster1 },
            configListeners: new[] { configListener },
            destinationResolver: syncExpandResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();

        await configManager.InitialLoadAsync();
        var configEvent = await configListener.Events.Reader.ReadAsync();
        var configLoadEvent = Assert.IsType<TestConfigChangeListener.ConfigurationLoadedEvent>(configEvent);

        Assert.True(configManager.TryGetCluster(cluster1.ClusterId, out var cluster));

        var expectedDestinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1-1", new DestinationConfig() { Address = "http://127.0.0.1:8080/1" } },
            { "d1-2", new DestinationConfig() { Address = "http://127.1.1.1:8080/1" } }
        };

        var actualDestinations = cluster.Destinations.ToDictionary(static k => k.Key, static v => v.Value.Model.Config);
        Assert.Equal(expectedDestinations, actualDestinations);

        // Trigger the change token and wait for a subsequent load
        var initialCts = cts[0];
        cts[0] = new();
        initialCts.Cancel();

        configEvent = await configListener.Events.Reader.ReadAsync();
        configLoadEvent = Assert.IsType<TestConfigChangeListener.ConfigurationLoadedEvent>(configEvent);

        Assert.True(configManager.TryGetCluster(cluster1.ClusterId, out cluster));

        expectedDestinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "d1-1", new DestinationConfig() { Address = "http://127.0.0.1:8080/2" } },
            { "d1-2", new DestinationConfig() { Address = "http://127.1.1.1:8080/2" } }
        };

        actualDestinations = cluster.Destinations.ToDictionary(static k => k.Key, static v => v.Value.Model.Config);
        Assert.Equal(expectedDestinations, actualDestinations);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Reload_ThrowsSync()
    {
        var configListener = new TestConfigChangeListener(includeApply: false);
        var cts = new CancellationTokenSource();
        var syncThrowResolver = new FakeDestinationResolver((destinations, cancellation) =>
        {
            if (cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Throwing!");
            }
            else
            {
                return new(new ResolvedDestinationCollection(destinations, new CancellationChangeToken(cts.Token)));
            }
        });
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(
            new List<RouteConfig>(),
            new List<ClusterConfig>() { cluster },
            configListeners: new[] { configListener },
            destinationResolver: syncThrowResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        await configManager.InitialLoadAsync();

        // Read the successful load event
        Assert.IsType<TestConfigChangeListener.ConfigurationLoadedEvent>(await configListener.Events.Reader.ReadAsync());

        // Trigger invalidation
        cts.Cancel();

        // Read the failure event
        var configLoadException = Assert.IsType<TestConfigChangeListener.ConfigurationLoadingFailedEvent>(await configListener.Events.Reader.ReadAsync());
        var ex = configLoadException.Exception;
        Assert.Equal("Throwing!", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_DestinationResolver_Reload_ThrowsAsync()
    {
        var configListener = new TestConfigChangeListener(includeApply: false);
        var cts = new CancellationTokenSource();
        var syncThrowResolver = new FakeDestinationResolver(async (destinations, cancellation) =>
        {
            await Task.Yield();

            if (cts.IsCancellationRequested)
            {
                throw new InvalidOperationException("Throwing!");
            }
            else
            {
                return new ResolvedDestinationCollection(destinations, new CancellationChangeToken(cts.Token));
            }
        });
        var cluster = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "d1", new DestinationConfig() { Address = "http://localhost" } }
            }
        };
        var services = CreateServices(
            new List<RouteConfig>(),
            new List<ClusterConfig>() { cluster },
            configListeners: new[] { configListener },
            destinationResolver: syncThrowResolver);
        var configManager = services.GetRequiredService<ProxyConfigManager>();
        await configManager.InitialLoadAsync();

        // Read the successful load event
        Assert.IsType<TestConfigChangeListener.ConfigurationLoadedEvent>(await configListener.Events.Reader.ReadAsync());

        // Trigger invalidation
        cts.Cancel();

        // Read the failure event
        var configLoadException = Assert.IsType<TestConfigChangeListener.ConfigurationLoadingFailedEvent>(await configListener.Events.Reader.ReadAsync());
        var innerExc1 = Assert.IsType<InvalidOperationException>(configLoadException.Exception);
        Assert.Equal("Error resolving destinations for cluster cluster1", innerExc1.Message);
        var innerExc2 = Assert.IsType<InvalidOperationException>(innerExc1.InnerException);
        Assert.Equal("Throwing!", innerExc2.Message);
    }
}
