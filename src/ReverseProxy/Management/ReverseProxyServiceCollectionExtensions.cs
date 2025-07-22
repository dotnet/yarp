// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Configuration.ConfigProvider;
using Yarp.ReverseProxy.Delegation;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Management;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.ServiceDiscovery;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/>
/// used to register the ReverseProxy's components.
/// </summary>
public static class ReverseProxyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IHttpForwarder"/> service for direct forwarding scenarios.
    /// </summary>
    public static IServiceCollection AddHttpForwarder(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHttpForwarder, HttpForwarder>();
        services.TryAddSingleton<ITransformBuilder, TransformBuilder>();

        services.AddSingleton<DirectForwardingHttpClientProvider>();

        return services;
    }

    /// <summary>
    /// Adds ReverseProxy's services to Dependency Injection.
    /// </summary>
    public static IReverseProxyBuilder AddReverseProxy(this IServiceCollection services)
    {
        var builder = new ReverseProxyBuilder(services);
        builder
            .AddConfigBuilder()
            .AddRuntimeStateManagers()
            .AddConfigManager()
            .AddSessionAffinityPolicies()
            .AddActiveHealthChecks()
            .AddPassiveHealthCheck()
            .AddLoadBalancingPolicies()
            .AddDestinationResolver()
            .AddProxy();

        if (OperatingSystem.IsWindows())
        {
            // Workaround for https://github.com/dotnet/aspnetcore/issues/59166.
            // .NET 9.0 packages for Ubuntu ship a broken Microsoft.AspNetCore.Server.HttpSys assembly.
            // Avoid loading types from that assembly on Linux unless the user explicitly tries to do so.
            builder.AddHttpSysDelegation();
        }
        else
        {
            // Add a no-op delegator in case someone is injecting the interface in their cross-plat logic.
            builder.Services.TryAddSingleton<IHttpSysDelegator, DummyHttpSysDelegator>();
        }

        services.TryAddSingleton<ProxyEndpointFactory>();

        services.AddDataProtection();
        services.AddAuthorization();
        services.AddCors();
        services.AddRouting();

        return builder;
    }

    /// <summary>
    /// Loads routes and endpoints from config.
    /// </summary>
    public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        builder.Services.AddSingleton<IProxyConfigProvider>(sp =>
        {
            // This is required because we're capturing the configuration via a closure
            return new ConfigurationConfigProvider(sp.GetRequiredService<ILogger<ConfigurationConfigProvider>>(), config);
        });

        return builder;
    }

    /// <summary>
    /// Registers a singleton IProxyConfigFilter service. Multiple filters are allowed, and they will be run in registration order.
    /// </summary>
    /// <typeparam name="TService">A class that implements IProxyConfigFilter.</typeparam>
    public static IReverseProxyBuilder AddConfigFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IReverseProxyBuilder builder) where TService : class, IProxyConfigFilter
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProxyConfigFilter, TService>());
        return builder;
    }

    /// <summary>
    /// Provides a callback that will be run for each route to conditionally add transforms.
    /// <see cref="AddTransforms(IReverseProxyBuilder, Action{TransformBuilderContext})"/> can be called multiple times to
    /// provide multiple callbacks.
    /// </summary>
    public static IReverseProxyBuilder AddTransforms(this IReverseProxyBuilder builder, Action<TransformBuilderContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        builder.Services.AddSingleton<ITransformProvider>(new ActionTransformProvider(action));
        return builder;
    }

    /// <summary>
    /// Provides a <see cref="ITransformProvider"/> implementation that will be run for each route to conditionally add transforms.
    /// <see cref="AddTransforms{T}(IReverseProxyBuilder)"/> can be called multiple times to provide multiple distinct types.
    /// </summary>
    public static IReverseProxyBuilder AddTransforms<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IReverseProxyBuilder builder) where T : class, ITransformProvider
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransformProvider, T>());
        return builder;
    }

    /// <summary>
    /// Adds a <see cref="ITransformFactory"/> implementation that will be used to read route transform config and generate
    /// the associated transform actions. <see cref="AddTransformFactory{T}(IReverseProxyBuilder)"/> can be called multiple
    /// times to provide multiple distinct types.
    /// </summary>
    public static IReverseProxyBuilder AddTransformFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IReverseProxyBuilder builder) where T : class, ITransformFactory
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransformFactory, T>());
        return builder;
    }

    /// <summary>
    /// Provides a callback to customize <see cref="SocketsHttpHandler"/> settings used for proxying requests.
    /// This will be called each time a cluster is added or changed. Cluster settings are applied to the handler before
    /// the callback. Custom data can be provided in the cluster metadata.
    /// </summary>
    public static IReverseProxyBuilder ConfigureHttpClient(this IReverseProxyBuilder builder, Action<ForwarderHttpClientContext, SocketsHttpHandler> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Avoid overriding any other custom factories. This does not handle the case where a IForwarderHttpClientFactory
        // is registered after this call.
        var service = builder.Services.FirstOrDefault(service => service.ServiceType == typeof(IForwarderHttpClientFactory));
        if (service is not null)
        {
            if (service.ImplementationType != typeof(ForwarderHttpClientFactory))
            {
                throw new InvalidOperationException($"ConfigureHttpClient will override the custom IForwarderHttpClientFactory type.");
            }
        }

        builder.Services.AddSingleton<IForwarderHttpClientFactory>(services =>
        {
            var logger = services.GetRequiredService<ILogger<ForwarderHttpClientFactory>>();
            return new CallbackHttpClientFactory(logger, configure);
        });
        return builder;
    }

    /// <summary>
    /// Provides a <see cref="IDestinationResolver"/> implementation which uses <see cref="System.Net.Dns"/> to resolve destinations.
    /// </summary>
    public static IReverseProxyBuilder AddDnsDestinationResolver(this IReverseProxyBuilder builder, Action<DnsDestinationResolverOptions>? configureOptions = null)
    {
        builder.Services.AddSingleton<IDestinationResolver, DnsDestinationResolver>();
        if (configureOptions is not null)
        {
            builder.Services.Configure(configureOptions);
        }

        return builder;
    }
}
