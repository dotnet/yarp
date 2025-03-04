// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Routing.Tests;

public class ProxyEndpointFactoryTests
{
    private IServiceProvider CreateServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ProxyEndpointFactory, ProxyEndpointFactory>();
        return serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_Works()
    {
        var services = CreateServices();
        _ = services.GetRequiredService<ProxyEndpointFactory>();
    }

    [Fact]
    public void AddEndpoint_HostAndPath_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Hosts = new[] { "example.com" },
                Path = "/a",
            },
            Order = 12,
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
        Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
        Assert.Equal(12, routeEndpoint.Order);
        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

        var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.NotNull(hostMetadata);
        Assert.Single(hostMetadata.Hosts);
        Assert.Equal("example.com", hostMetadata.Hosts[0]);
    }

    private (RouteEndpoint routeEndpoint, RouteModel routeConfig) CreateEndpoint(ProxyEndpointFactory factory, RouteState routeState, RouteConfig routeConfig, ClusterState clusterState)
    {
        routeState.ClusterRevision = clusterState.Revision;
        var routeModel = new RouteModel(routeConfig, clusterState, HttpTransformer.Default);

        var endpoint = factory.CreateEndpoint(routeModel, Array.Empty<Action<EndpointBuilder>>());

        var routeEndpoint = Assert.IsType<RouteEndpoint>(endpoint);

        return (routeEndpoint, routeModel);
    }

    [Fact]
    public void AddEndpoint_JustHost_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Hosts = new[] { "example.com" },
            },
            Order = 12,
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
        Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
        Assert.Equal(12, routeEndpoint.Order);
        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

        var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.NotNull(hostMetadata);
        Assert.Single(hostMetadata.Hosts);
        Assert.Equal("example.com", hostMetadata.Hosts[0]);
    }

    [Fact]
    public void AddEndpoint_JustHostWithWildcard_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Hosts = new[] { "*.example.com" },
            },
            Order = 12,
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
        Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
        Assert.Equal(12, routeEndpoint.Order);
        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

        var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.NotNull(hostMetadata);
        Assert.Single(hostMetadata.Hosts);
        Assert.Equal("*.example.com", hostMetadata.Hosts[0]);
    }

    [Fact]
    public void AddEndpoint_JustPath_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/a",
            },
            Order = 12,
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
        Assert.Equal("/a", routeEndpoint.RoutePattern.RawText);
        Assert.Equal(12, routeEndpoint.Order);
        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

        var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.Null(hostMetadata);
    }

    [Fact]
    public void AddEndpoint_NullMatchers_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Order = 12,
            Match = new RouteMatch()
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        Assert.Same(routeConfig, routeEndpoint.Metadata.GetMetadata<RouteModel>());
        Assert.Equal("/{**catchall}", routeEndpoint.RoutePattern.RawText);
        Assert.Equal(12, routeEndpoint.Order);
        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));

        var hostMetadata = routeEndpoint.Metadata.GetMetadata<HostAttribute>();
        Assert.Null(hostMetadata);
    }

    [Fact]
    public void AddEndpoint_InvalidPath_BubblesOutException()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/{invalid",
            },
            Order = 12,
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        Action action = () => CreateEndpoint(factory, routeState, route, cluster);

        Assert.Throws<RoutePatternException>(action);
    }

    [Fact]
    public void AddEndpoint_DefaultAuth_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = "defaulT",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Null(attribute.Policy);
    }

    [Fact]
    public void AddEndpoint_AnonymousAuth_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = "AnonymouS",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.IsType<AllowAnonymousAttribute>(routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Fact]
    public void AddEndpoint_CustomAuth_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            AuthorizationPolicy = "custom",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = Assert.IsType<AuthorizeAttribute>(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Equal("custom", attribute.Policy);
    }

    [Fact]
    public void AddEndpoint_NoAuth_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Null(routeEndpoint.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Fact]
    public void AddEndpoint_DefaultRateLimiter_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            RateLimiterPolicy = "defaulT",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Null(routeEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
    }

    [Fact]
    public void AddEndpoint_CustomRateLimiter_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            RateLimiterPolicy = "custom",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = routeEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("custom", attribute.PolicyName);
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
    }

    [Fact]
    public void AddEndpoint_DisableRateLimiter_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            RateLimiterPolicy = "disAble",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.NotNull(routeEndpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
    }

    [Fact]
    public void AddEndpoint_NoRateLimiter_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Null(routeEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>());
    }

    [Fact]
    public void AddEndpoint_CustomTimeoutPolicy_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            TimeoutPolicy = "custom",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = routeEndpoint.Metadata.GetMetadata<RequestTimeoutAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("custom", attribute.PolicyName);
        Assert.Null(attribute.Timeout);
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRequestTimeoutAttribute>());
    }

    [Fact]
    public void AddEndpoint_CustomTimeout_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Timeout = TimeSpan.FromSeconds(5),
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = routeEndpoint.Metadata.GetMetadata<RequestTimeoutAttribute>();
        Assert.NotNull(attribute);
        Assert.Null(attribute.PolicyName);
        Assert.Equal(TimeSpan.FromSeconds(5), attribute.Timeout);
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRequestTimeoutAttribute>());
    }

    [Fact]
    public void AddEndpoint_DisableTimeoutPolicy_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            TimeoutPolicy = "disAble",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.NotNull(routeEndpoint.Metadata.GetMetadata<DisableRequestTimeoutAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<RequestTimeoutAttribute>());
    }

    [Fact]
    public void AddEndpoint_NoTimeoutPolicy_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Null(routeEndpoint.Metadata.GetMetadata<RequestTimeoutAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<DisableRequestTimeoutAttribute>());
    }

    [Fact]
    public void AddEndpoint_DefaultCors_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = "defaulT",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
        Assert.Null(attribute.PolicyName);
        Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
    }

    [Fact]
    public void AddEndpoint_CustomCors_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = "custom",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        var attribute = Assert.IsType<EnableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
        Assert.Equal("custom", attribute.PolicyName);
        Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
    }

    [Fact]
    public void AddEndpoint_DisableCors_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            CorsPolicy = "disAble",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.IsType<DisableCorsAttribute>(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
    }

    [Fact]
    public void AddEndpoint_NoCors_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Order = 12,
            Match = new RouteMatch(),
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, _) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Null(routeEndpoint.Metadata.GetMetadata<IEnableCorsAttribute>());
        Assert.Null(routeEndpoint.Metadata.GetMetadata<IDisableCorsAttribute>());
    }

    [Fact]
    public void BuildEndpoints_Header_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "header1",
                        Values = new[] { "value1" },
                        Mode = HeaderMatchMode.HeaderPrefix,
                        IsCaseSensitive = true,
                    }
                }
            },
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        var headerMetadata = routeEndpoint.Metadata.GetMetadata<IHeaderMetadata>();
        Assert.NotNull(headerMetadata);
        var matchers = headerMetadata.Matchers;
        Assert.Single(matchers);
        var matcher = matchers.Single();
        Assert.Equal("header1", matcher.Name);
        Assert.Equal(new[] { "value1" }, matcher.Values);
        Assert.Equal(HeaderMatchMode.HeaderPrefix, matcher.Mode);
        Assert.Equal(StringComparison.Ordinal, matcher.Comparison);

        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));
    }

    [Fact]
    public void BuildEndpoints_Headers_Works()
    {
        var services = CreateServices();
        var factory = services.GetRequiredService<ProxyEndpointFactory>();
        factory.SetProxyPipeline(context => Task.CompletedTask);

        var route = new RouteConfig
        {
            RouteId = "route1",
            Match = new RouteMatch
            {
                Path = "/",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "header1",
                        Values = new[] { "value1" },
                        Mode = HeaderMatchMode.HeaderPrefix,
                        IsCaseSensitive = true,
                    },
                    new RouteHeader()
                    {
                        Name = "header2",
                        Mode = HeaderMatchMode.Exists,
                    }
                }
            },
        };
        var cluster = new ClusterState("cluster1");
        var routeState = new RouteState("route1");

        var (routeEndpoint, routeConfig) = CreateEndpoint(factory, routeState, route, cluster);

        Assert.Same(cluster, routeConfig.Cluster);
        Assert.Equal("route1", routeEndpoint.DisplayName);
        var metadata = routeEndpoint.Metadata.GetMetadata<IHeaderMetadata>();
        Assert.Equal(2, metadata.Matchers.Length);

        var firstMetadata = metadata.Matchers.First();
        Assert.NotNull(firstMetadata);
        Assert.Equal("header1", firstMetadata.Name);
        Assert.Equal(new[] { "value1" }, firstMetadata.Values);
        Assert.Equal(HeaderMatchMode.HeaderPrefix, firstMetadata.Mode);
        Assert.Equal(StringComparison.Ordinal, firstMetadata.Comparison);

        var secondMetadata = metadata.Matchers.Skip(1).Single();
        Assert.NotNull(secondMetadata);
        Assert.Equal("header2", secondMetadata.Name);
        Assert.Same(Array.Empty<string>(), secondMetadata.Values);
        Assert.Equal(HeaderMatchMode.Exists, secondMetadata.Mode);
        Assert.Equal(StringComparison.OrdinalIgnoreCase, secondMetadata.Comparison);

        Assert.False(routeConfig.HasConfigChanged(route, cluster, routeState.ClusterRevision));
    }
}
