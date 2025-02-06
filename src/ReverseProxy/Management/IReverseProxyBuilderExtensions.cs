// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Configuration.ClusterValidators;
using Yarp.ReverseProxy.Configuration.RouteValidators;
using Yarp.ReverseProxy.Delegation;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Routing;
using Yarp.ReverseProxy.ServiceDiscovery;
using Yarp.ReverseProxy.SessionAffinity;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Management;

internal static class IReverseProxyBuilderExtensions
{
    public static IReverseProxyBuilder AddConfigBuilder(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IYarpRateLimiterPolicyProvider, YarpRateLimiterPolicyProvider>();
        builder.Services.TryAddSingleton<IYarpOutputCachePolicyProvider, YarpOutputCachePolicyProvider>();
        builder.Services.TryAddSingleton<IConfigValidator, ConfigValidator>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, AuthorizationPolicyValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, RateLimitPolicyValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, OutputCachePolicyValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, TimeoutPolicyValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, CorsPolicyValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, HeadersValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, HostValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, MethodsValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, PathValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IRouteValidator, QueryParametersValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, DestinationValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, LoadBalancingValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, HealthCheckValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, SessionAffinityValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, ProxyHttpClientValidator>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IClusterValidator, ProxyHttpRequestValidator>());
        builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();
        builder.AddTransformFactory<ForwardedTransformFactory>();
        builder.AddTransformFactory<HttpMethodTransformFactory>();
        builder.AddTransformFactory<PathTransformFactory>();
        builder.AddTransformFactory<QueryTransformFactory>();
        builder.AddTransformFactory<RequestHeadersTransformFactory>();
        builder.AddTransformFactory<ResponseTransformFactory>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, HeaderMatcherPolicy>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, QueryParameterMatcherPolicy>());
        return builder;
    }

    public static IReverseProxyBuilder AddRuntimeStateManagers(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IDestinationHealthUpdater, DestinationHealthUpdater>();

        builder.Services.TryAddSingleton<IClusterDestinationsUpdater, ClusterDestinationsUpdater>();
        builder.Services.TryAddEnumerable(new[] {
            ServiceDescriptor.Singleton<IAvailableDestinationsPolicy, HealthyAndUnknownDestinationsPolicy>(),
            ServiceDescriptor.Singleton<IAvailableDestinationsPolicy, HealthyOrPanicDestinationsPolicy>()
        });
        return builder;
    }

    public static IReverseProxyBuilder AddConfigManager(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<ProxyConfigManager>();
        builder.Services.TryAddSingleton<IProxyStateLookup>(sp => sp.GetRequiredService<ProxyConfigManager>());
        return builder;
    }

    public static IReverseProxyBuilder AddProxy(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IForwarderHttpClientFactory, ForwarderHttpClientFactory>();

        builder.Services.AddHttpForwarder();
        return builder;
    }

    public static IReverseProxyBuilder AddLoadBalancingPolicies(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IRandomFactory, RandomFactory>();

        builder.Services.TryAddEnumerable(new[] {
            ServiceDescriptor.Singleton<ILoadBalancingPolicy, FirstLoadBalancingPolicy>(),
            ServiceDescriptor.Singleton<ILoadBalancingPolicy, LeastRequestsLoadBalancingPolicy>(),
            ServiceDescriptor.Singleton<ILoadBalancingPolicy, RandomLoadBalancingPolicy>(),
            ServiceDescriptor.Singleton<ILoadBalancingPolicy, PowerOfTwoChoicesLoadBalancingPolicy>(),
            ServiceDescriptor.Singleton<ILoadBalancingPolicy, RoundRobinLoadBalancingPolicy>()
        });

        return builder;
    }

    public static IReverseProxyBuilder AddSessionAffinityPolicies(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddEnumerable(new[] {
            ServiceDescriptor.Singleton<IAffinityFailurePolicy, RedistributeAffinityFailurePolicy>(),
            ServiceDescriptor.Singleton<IAffinityFailurePolicy, Return503ErrorAffinityFailurePolicy>()
        });
        builder.Services.TryAddEnumerable(new[] {
            ServiceDescriptor.Singleton<ISessionAffinityPolicy, CookieSessionAffinityPolicy>(),
            ServiceDescriptor.Singleton<ISessionAffinityPolicy, HashCookieSessionAffinityPolicy>(),
            ServiceDescriptor.Singleton<ISessionAffinityPolicy, ArrCookieSessionAffinityPolicy>(),
            ServiceDescriptor.Singleton<ISessionAffinityPolicy, CustomHeaderSessionAffinityPolicy>()
        });
        builder.AddTransforms<AffinitizeTransformProvider>();

        return builder;
    }

    public static IReverseProxyBuilder AddActiveHealthChecks(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IProbingRequestFactory, DefaultProbingRequestFactory>();

        // Avoid registering several IActiveHealthCheckMonitor implementations.
        if (!builder.Services.Any(d => d.ServiceType == typeof(IActiveHealthCheckMonitor)))
        {
            builder.Services.AddSingleton<ActiveHealthCheckMonitor>();
            builder.Services.AddSingleton<IActiveHealthCheckMonitor>(p =>
                p.GetRequiredService<ActiveHealthCheckMonitor>());
            builder.Services.AddSingleton<IClusterChangeListener>(p =>
                p.GetRequiredService<ActiveHealthCheckMonitor>());
        }

        builder.Services.AddSingleton<IActiveHealthCheckPolicy, ConsecutiveFailuresHealthPolicy>();
        return builder;
    }

    public static IReverseProxyBuilder AddPassiveHealthCheck(this IReverseProxyBuilder builder)
    {
        builder.Services.AddSingleton<IPassiveHealthCheckPolicy, TransportFailureRateHealthPolicy>();
        return builder;
    }

    public static IReverseProxyBuilder AddHttpSysDelegation(this IReverseProxyBuilder builder)
    {
        builder.Services.AddSingleton<HttpSysDelegator>();
        builder.Services.TryAddSingleton<IHttpSysDelegator>(p => p.GetRequiredService<HttpSysDelegator>());
        builder.Services.AddSingleton<IClusterChangeListener>(p => p.GetRequiredService<HttpSysDelegator>());

        return builder;
    }

    public static IReverseProxyBuilder AddDestinationResolver(this IReverseProxyBuilder builder)
    {
        builder.Services.TryAddSingleton<IDestinationResolver, NoOpDestinationResolver>();
        return builder;
    }
}
