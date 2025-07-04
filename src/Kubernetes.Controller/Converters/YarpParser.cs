// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using k8s.Models;
using YamlDotNet.Serialization;
using Yarp.ReverseProxy.Configuration;
using Yarp.Kubernetes.Controller.Caching;
using System.Runtime.InteropServices;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Kubernetes.Controller.Converters;

internal static class YarpParser
{
    private const string ExternalNameServiceType = "ExternalName";
    private static readonly Deserializer YamlDeserializer = new();

    internal static void ConvertFromKubernetesIngress(YarpIngressContext ingressContext, YarpConfigContext configContext)
    {
        var spec = ingressContext.Ingress.Spec;
        var defaultBackend = spec?.DefaultBackend;
        var defaultService = defaultBackend?.Service;
        IList<V1EndpointSubset> defaultSubsets = default;

        if (!string.IsNullOrEmpty(defaultService?.Name))
        {
            defaultSubsets = ingressContext.Endpoints.SingleOrDefault(x => x.Name == defaultService?.Name).Subsets;
        }

        // cluster can contain multiple replicas for each destination, need to know the lookup base don endpoints
        var options = HandleAnnotations(ingressContext, ingressContext.Ingress.Metadata);

        foreach (var rule in spec?.Rules ?? Enumerable.Empty<V1IngressRule>())
        {
            HandleIngressRule(ingressContext, ingressContext.Endpoints, defaultSubsets, rule, configContext);
        }
    }

    private static void HandleIngressRule(YarpIngressContext ingressContext, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule, YarpConfigContext configContext)
    {
        var http = rule.Http;
        foreach (var path in http.Paths ?? Enumerable.Empty<V1HTTPIngressPath>())
        {
            var service = ingressContext.Services.SingleOrDefault(s => s.Metadata.Name == path.Backend.Service.Name);
            if (service.Spec != null)
            {
                if (string.Equals(service.Spec.Type, ExternalNameServiceType, StringComparison.OrdinalIgnoreCase))
                {
                    HandleExternalIngressRulePath(ingressContext, service.Spec.ExternalName, rule, path, configContext);
                }
                else
                {
                    var servicePort = service.Spec.Ports.SingleOrDefault(p => MatchesPort(p, path.Backend.Service.Port));
                    if (servicePort != null)
                    {
                        HandleIngressRulePath(ingressContext, servicePort, endpoints, defaultSubsets, rule, path, configContext);
                    }
                }
            }
        }
    }

    private static void HandleExternalIngressRulePath(YarpIngressContext ingressContext, string externalName, V1IngressRule rule, V1HTTPIngressPath path, YarpConfigContext configContext)
    {
        var backend = path.Backend;
        var ingressServiceBackend = backend.Service;
        var routes = configContext.Routes;

        var cluster = GetOrAddCluster(ingressContext, configContext, ingressServiceBackend);

        var pathMatch = FixupPathMatch(path);
        var host = rule.Host;

        routes.Add(CreateRoute(ingressContext, path, cluster, pathMatch, host));
        AddDestination(cluster, ingressContext, externalName, ingressServiceBackend.Port.Number);
    }

    private static void HandleIngressRulePath(YarpIngressContext ingressContext, V1ServicePort servicePort, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule, V1HTTPIngressPath path, YarpConfigContext configContext)
    {
        var backend = path.Backend;
        var ingressServiceBackend = backend.Service;
        var subsets = defaultSubsets;
        var routes = configContext.Routes;

        if (!string.IsNullOrEmpty(ingressServiceBackend?.Name))
        {
            subsets = endpoints.SingleOrDefault(x => x.Name == ingressServiceBackend?.Name).Subsets;
        }

        var cluster = GetOrAddCluster(ingressContext, configContext, ingressServiceBackend);

        // make sure cluster is present
        foreach (var subset in subsets ?? Enumerable.Empty<V1EndpointSubset>())
        {
            var isRoutePresent = false;
            foreach (var port in subset.Ports ?? Enumerable.Empty<Corev1EndpointPort>())
            {
                if (!MatchesPort(port, servicePort))
                {
                    continue;
                }

                if (!isRoutePresent)
                {
                    var pathMatch = FixupPathMatch(path);
                    var host = rule.Host;
                    routes.Add(CreateRoute(ingressContext, path, cluster, pathMatch, host));
                    isRoutePresent = true;
                }

                // Add destination for every endpoint address
                foreach (var address in subset.Addresses ?? Enumerable.Empty<V1EndpointAddress>())
                {
                    AddDestination(cluster, ingressContext, address.Ip, port.Port);
                }
            }
        }
    }

    private static void AddDestination(ClusterTransfer cluster, YarpIngressContext ingressContext, string host, int? port)
    {
        var isHttps =
            ingressContext.Options.Https ||
            cluster.ClusterId.EndsWith(":443", StringComparison.Ordinal) ||
            cluster.ClusterId.EndsWith(":https", StringComparison.OrdinalIgnoreCase);

        var protocol = isHttps ? "https" : "http";

        var uri = $"{protocol}://{host}";
        if (port.HasValue)
        {
            uri += $":{port}";
        }
        cluster.Destinations[uri] = new DestinationConfig()
        {
            Address = uri
        };
    }

    private static RouteConfig CreateRoute(YarpIngressContext ingressContext, V1HTTPIngressPath path, ClusterTransfer cluster, string pathMatch, string host)
    {
        return new RouteConfig()
        {
            Match = new RouteMatch()
            {
                Methods = ingressContext.Options.RouteMethods,
                Hosts = host is not null ? new[] { host } : Array.Empty<string>(),
                Path = pathMatch,
                Headers = ingressContext.Options.RouteHeaders,
                QueryParameters = ingressContext.Options.RouteQueryParameters
            },
            ClusterId = cluster.ClusterId,
            RouteId = $"{ingressContext.Ingress.Metadata.Name}.{ingressContext.Ingress.Metadata.NamespaceProperty}:{host}{path.Path}",
            Transforms = ingressContext.Options.Transforms,
            AuthorizationPolicy = ingressContext.Options.AuthorizationPolicy,
            RateLimiterPolicy = ingressContext.Options.RateLimiterPolicy,
            OutputCachePolicy = ingressContext.Options.OutputCachePolicy,
            Timeout = ingressContext.Options.Timeout,
            TimeoutPolicy = ingressContext.Options.TimeoutPolicy,
            CorsPolicy = ingressContext.Options.CorsPolicy,
            Metadata = ingressContext.Options.RouteMetadata,
            Order = ingressContext.Options.RouteOrder,
        };
    }

    private static ClusterTransfer GetOrAddCluster(YarpIngressContext ingressContext, YarpConfigContext configContext, V1IngressServiceBackend ingressServiceBackend)
    {
        var clusters = configContext.ClusterTransfers;
        // Each ingress rule path can only be for one service
        var key = UpstreamName(ingressContext.Ingress.Metadata.NamespaceProperty, ingressServiceBackend);
        var cluster = CollectionsMarshal.GetValueRefOrAddDefault(clusters, key, out _) ??= new ClusterTransfer();
        cluster.ClusterId = key;
        cluster.LoadBalancingPolicy = ingressContext.Options.LoadBalancingPolicy;
        cluster.SessionAffinity = ingressContext.Options.SessionAffinity;
        cluster.HealthCheck = ingressContext.Options.HealthCheck;
        cluster.HttpClientConfig = ingressContext.Options.HttpClientConfig;
        cluster.HttpRequest = ingressContext.Options.HttpRequest;
        return cluster;
    }

    private static string UpstreamName(string namespaceName, V1IngressServiceBackend ingressServiceBackend)
    {
        if (ingressServiceBackend is not null)
        {
            if (ingressServiceBackend.Port.Number.HasValue && ingressServiceBackend.Port.Number.Value > 0)
            {
                return $"{ingressServiceBackend.Name}.{namespaceName}:{ingressServiceBackend.Port.Number}";
            }

            if (!string.IsNullOrWhiteSpace(ingressServiceBackend.Port.Name))
            {
                return $"{ingressServiceBackend.Name}.{namespaceName}:{ingressServiceBackend.Port.Name}";
            }
        }

        return $"{namespaceName}-INVALID";
    }

    private static string FixupPathMatch(V1HTTPIngressPath path)
    {
        var pathMatch = path.Path;

        // Prefix match is the default for implementation specific.
        if (string.Equals(path.PathType, "Prefix", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path.PathType, "ImplementationSpecific", StringComparison.OrdinalIgnoreCase))
        {
            if (!pathMatch.EndsWith('/'))
            {
                pathMatch += "/";
            }
            // remember for prefix matches, /foo/ works for either /foo or /foo/
            pathMatch += "{**catch-all}";
        }

        return pathMatch;
    }

    private static YarpIngressOptions HandleAnnotations(YarpIngressContext context, V1ObjectMeta metadata)
    {
        var options = context.Options;
        var annotations = metadata.Annotations;
        if (annotations is null)
        {
            return options;
        }

        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/backend-protocol", out var http))
        {
            options.Https = http.Equals("https", StringComparison.OrdinalIgnoreCase);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/transforms", out var transforms))
        {
            options.Transforms = YamlDeserializer.Deserialize<List<Dictionary<string, string>>>(transforms);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/authorization-policy", out var authorizationPolicy))
        {
            options.AuthorizationPolicy = authorizationPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/rate-limiter-policy", out var rateLimiterPolicy))
        {
            options.RateLimiterPolicy = rateLimiterPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/output-cache-policy", out var outputCachePolicy))
        {
            options.OutputCachePolicy = outputCachePolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/timeout", out var timeout))
        {
            options.Timeout = TimeSpan.Parse(timeout, CultureInfo.InvariantCulture);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/timeout-policy", out var timeoutPolicy))
        {
            options.TimeoutPolicy = timeoutPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/cors-policy", out var corsPolicy))
        {
            options.CorsPolicy = corsPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/session-affinity", out var sessionAffinity))
        {
            options.SessionAffinity = YamlDeserializer.Deserialize<SessionAffinityConfig>(sessionAffinity);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/load-balancing", out var loadBalancing))
        {
            options.LoadBalancingPolicy = loadBalancing;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/http-client", out var httpClientConfig))
        {
            options.HttpClientConfig = YamlDeserializer.Deserialize<HttpClientConfig>(httpClientConfig);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/http-request", out var httpRequest))
        {
            options.HttpRequest = YamlDeserializer.Deserialize<ForwarderRequestConfig>(httpRequest);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/health-check", out var healthCheck))
        {
            options.HealthCheck = YamlDeserializer.Deserialize<HealthCheckConfig>(healthCheck);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-metadata", out var routeMetadata))
        {
            options.RouteMetadata = YamlDeserializer.Deserialize<Dictionary<string, string>>(routeMetadata);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-headers", out var routeHeaders))
        {
            // YamlDeserializer does not support IReadOnlyList<string> in RouteHeader for now, so we use RouteHeaderWrapper to solve this problem.
            options.RouteHeaders = YamlDeserializer.Deserialize<List<RouteHeaderWrapper>>(routeHeaders).Select(p => p.ToRouteHeader()).ToList();
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-queryparameters", out var routeQueryParameters))
        {
            // YamlDeserializer does not support IReadOnlyList<string> in RouteParameters for now, so we use RouterQueryParameterWrapper to solve this problem.
            options.RouteQueryParameters = YamlDeserializer.Deserialize<List<RouteQueryParameterWrapper>>(routeQueryParameters).Select(p => p.ToRouteQueryParameter()).ToList();
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-order", out var routeOrder))
        {
            options.RouteOrder = int.Parse(routeOrder, CultureInfo.InvariantCulture);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-methods", out var routeMethods))
        {
            options.RouteMethods = YamlDeserializer.Deserialize<List<string>>(routeMethods);
        }
        // metadata to support:
        // rewrite target
        // auth
        // http or https
        // default backend
        // CORS
        // GRPC
        // HTTP2
        // Connection limits
        // rate limits

        // backend health checks.
        return options;
    }

    private static bool MatchesPort(Corev1EndpointPort port1, V1ServicePort port2)
    {
        if (port1 is null || port2?.TargetPort is null)
        {
            return false;
        }
        if (int.TryParse(port2.TargetPort, out var port2Number) && port2Number == port1.Port)
        {
            return true;
        }
        if (string.Equals(port2.Name, port1.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private static bool MatchesPort(V1ServicePort port1, V1ServiceBackendPort port2)
    {
        if (port1 is null || port2 is null)
        {
            return false;
        }
        if (port2.Number is not null && port2.Number == port1.Port)
        {
            return true;
        }
        if (port2.Name is not null && string.Equals(port2.Name, port1.Name, StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }
}
