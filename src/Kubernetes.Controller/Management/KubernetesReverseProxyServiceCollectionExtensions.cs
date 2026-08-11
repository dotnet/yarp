// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Certificates;
using Yarp.Kubernetes.Controller.Client;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.Kubernetes.Controller.Controllers;
using Yarp.Kubernetes.Controller.Dispatching;
using Yarp.Kubernetes.Controller.Protocol;
using Yarp.Kubernetes.Controller.Services;
using Yarp.ReverseProxy.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/>
/// used to register the Kubernetes-based ReverseProxy's components.
/// </summary>
public static class KubernetesReverseProxyServiceCollectionExtensions
{
    /// <summary>
    /// Adds ReverseProxy's services to Dependency Injection.
    /// </summary>
    /// <param name="services">Dependency injection registration.</param>
    /// <param name="config">Application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IReverseProxyBuilder AddKubernetesReverseProxy(this IServiceCollection services, IConfiguration config)
    {
        // Add components from the kubernetes controller framework
        services.AddKubernetesControllerRuntime(config);

        // Add the in-memory configuration cache.
        var provider = new KubernetesConfigProvider();
        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IUpdateConfig>(provider);

        return services.AddReverseProxy();
    }

    /// <summary>
    /// Adds an ingress controller that monitors for Ingress resource changes and notifies a Yarp "Ingress" application.
    /// </summary>
    /// <param name="services">Dependency injection registration.</param>
    /// <param name="config">Application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKubernetesIngressMonitor(this IServiceCollection services, IConfiguration config)
    {
        // Add components from the kubernetes controller framework
        services.AddKubernetesControllerRuntime(config);

        // Add the dispatcher for the Ingress application to connect to.
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.AddSingleton<IUpdateConfig, DispatchConfigProvider>();

        return services;
    }

    /// <summary>
    /// Adds the dispatching controller that allows a Yarp "Ingress" application to monitor for changes.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>The same <see cref="IMvcBuilder"/> for chaining.</returns>
    public static IMvcBuilder AddKubernetesDispatchController(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(typeof(DispatchController).Assembly);
    }

    public static IServiceCollection AddKubernetesControllerRuntime(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        // Add components implemented by this application
        services.AddHostedService<IngressController>();
        services.AddSingleton<ICache, IngressCache>();
        services.AddTransient<IReconciler, Reconciler>();
        services.Configure<YarpOptions>(config.GetSection("Yarp"));

        // Register the necessary Kubernetes resource informers
        services.RegisterResourceInformer<V1Ingress, V1IngressResourceInformer>();
        services.RegisterResourceInformer<V1Service, V1ServiceResourceInformer>();
        services.RegisterResourceInformer<V1Endpoints, V1EndpointsResourceInformer>();
        services.RegisterResourceInformer<V1IngressClass, V1IngressClassResourceInformer>();

        // We should only retrieve secrets we might be interested in (because Helm V3, for example, can generate lots of secrets)
        services.RegisterResourceInformer<V1Secret, V1SecretResourceInformer>("type=kubernetes.io/tls");

        // Add the Ingress/Secret to certificate management
        services.AddSingleton<ServerCertificateSelector>();
        services.AddHostedService(x => x.GetRequiredService<ServerCertificateSelector>());
        services.AddSingleton<IServerCertificateSelector>(x => x.GetRequiredService<ServerCertificateSelector>());
        services.AddSingleton<ICertificateHelper, CertificateHelper>();

        // ingress status updater
        services.AddSingleton<IIngressResourceStatusUpdater, V1IngressResourceStatusUpdater>();

        return services.AddKubernetesCore();
    }

    /// <summary>
    /// Registers the resource informer.
    /// </summary>
    /// <typeparam name="TResource">The type of the t related resource.</typeparam>
    /// <typeparam name="TService">The implementation type of the resource informer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection RegisterResourceInformer<TResource, TService>(this IServiceCollection services)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
        where TService : IResourceInformer<TResource>
    {
        return services.RegisterResourceInformer<TResource, TService>(null);
    }

    /// <summary>
    /// Registers the resource informer with a field selector.
    /// </summary>
    /// <typeparam name="TResource">The type of the t related resource.</typeparam>
    /// <typeparam name="TService">The implementation type of the resource informer.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="fieldSelector">A field selector to constrain the resources the informer retrieves.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection RegisterResourceInformer<TResource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services, string fieldSelector)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
        where TService : IResourceInformer<TResource>
    {
        services.AddSingleton(new ResourceSelector<TResource>(fieldSelector));
        services.AddSingleton(typeof(IResourceInformer<TResource>), typeof(TService));

        return services.RegisterHostedService<IResourceInformer<TResource>>();
    }
}
