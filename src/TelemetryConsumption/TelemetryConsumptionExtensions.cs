// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection;

public static class TelemetryConsumptionExtensions
{
    /// <summary>
    /// Registers all telemetry listeners (Forwarder, Kestrel, Http, NameResolution, NetSecurity, Sockets and WebSockets).
    /// </summary>
    public static IServiceCollection AddTelemetryListeners(this IServiceCollection services)
    {
        services.AddHostedService<WebSocketsEventListenerService>();
        services.AddHostedService<ForwarderEventListenerService>();
        services.AddHostedService<KestrelEventListenerService>();
        services.AddHostedService<HttpEventListenerService>();
        services.AddHostedService<NameResolutionEventListenerService>();
        services.AddHostedService<NetSecurityEventListenerService>();
        services.AddHostedService<SocketsEventListenerService>();
        return services;
    }

    /// <summary>
    /// Registers a consumer singleton for every I*TelemetryConsumer interface it implements.
    /// </summary>
    public static IServiceCollection AddTelemetryConsumer(this IServiceCollection services, object consumer)
    {
        var implementsAny = false;

        if (consumer is IWebSocketsTelemetryConsumer webSocketsTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(webSocketsTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is IForwarderTelemetryConsumer forwarderTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(forwarderTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is IKestrelTelemetryConsumer kestrelTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(kestrelTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is IHttpTelemetryConsumer httpTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(httpTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is INameResolutionTelemetryConsumer nameResolutionTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(nameResolutionTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is INetSecurityTelemetryConsumer netSecurityTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(netSecurityTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is ISocketsTelemetryConsumer socketsTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(socketsTelemetryConsumer));
            implementsAny = true;
        }

        if (!implementsAny)
        {
            throw new ArgumentException("The consumer must implement at least one I*TelemetryConsumer interface.", nameof(consumer));
        }

        services.AddTelemetryListeners();

        return services;
    }

    /// <summary>
    /// Registers a <typeparamref name="TConsumer"/> singleton for every I*TelemetryConsumer interface it implements.
    /// </summary>
    public static IServiceCollection AddTelemetryConsumer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConsumer>(this IServiceCollection services)
        where TConsumer : class
    {
        var implementsAny = false;

        if (typeof(IWebSocketsTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IWebSocketsTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IForwarderTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IKestrelTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IHttpTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(INameResolutionTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(INetSecurityTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(ISocketsTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (!implementsAny)
        {
            throw new ArgumentException("TConsumer must implement at least one I*TelemetryConsumer interface.", nameof(TConsumer));
        }

        services.TryAddSingleton<TConsumer>();

        services.AddTelemetryListeners();

        return services;
    }

    /// <summary>
    /// Registers a consumer singleton for every IMetricsConsumer interface it implements.
    /// </summary>
    public static IServiceCollection AddMetricsConsumer(this IServiceCollection services, object consumer)
    {
        var implementsAny = false;

        if (consumer is IMetricsConsumer<ForwarderMetrics> forwarderMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(forwarderMetricsConsumer));
            implementsAny = true;
        }

        if (consumer is IMetricsConsumer<KestrelMetrics> kestrelMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(kestrelMetricsConsumer));
            implementsAny = true;
        }

        if (consumer is IMetricsConsumer<HttpMetrics> httpMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(httpMetricsConsumer));
            implementsAny = true;
        }

        if (consumer is IMetricsConsumer<NameResolutionMetrics> nameResolutionMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(nameResolutionMetricsConsumer));
            implementsAny = true;
        }

        if (consumer is IMetricsConsumer<NetSecurityMetrics> netSecurityMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(netSecurityMetricsConsumer));
            implementsAny = true;
        }

        if (consumer is IMetricsConsumer<SocketsMetrics> socketsMetricsConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(socketsMetricsConsumer));
            implementsAny = true;
        }

        if (!implementsAny)
        {
            throw new ArgumentException("The consumer must implement at least one IMetricsConsumer interface.", nameof(consumer));
        }

        services.AddTelemetryListeners();

        return services;
    }

    /// <summary>
    /// Registers a consumer singleton for every IMetricsConsumer interface it implements.
    /// </summary>
    public static IServiceCollection AddMetricsConsumer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConsumer>(this IServiceCollection services)
        where TConsumer : class
    {
        var implementsAny = false;

        if (typeof(IMetricsConsumer<ForwarderMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<ForwarderMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IMetricsConsumer<KestrelMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<KestrelMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IMetricsConsumer<HttpMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<HttpMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IMetricsConsumer<NameResolutionMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<NameResolutionMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IMetricsConsumer<NetSecurityMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<NetSecurityMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IMetricsConsumer<SocketsMetrics>).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IMetricsConsumer<SocketsMetrics>)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (!implementsAny)
        {
            throw new ArgumentException("TConsumer must implement at least one IMetricsConsumer interface.", nameof(TConsumer));
        }

        services.TryAddSingleton<TConsumer>();

        services.AddTelemetryListeners();

        return services;
    }
}
