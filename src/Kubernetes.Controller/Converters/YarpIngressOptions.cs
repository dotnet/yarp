// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Kubernetes.Controller.Converters;

internal sealed class YarpIngressOptions
{
    public bool Https { get; set; }
    public List<Dictionary<string, string>> Transforms { get; set; }
    public string AuthorizationPolicy { get; set; }
    public string RateLimiterPolicy { get; set; }
    public string OutputCachePolicy { get; set; }
    public SessionAffinityConfig SessionAffinity { get; set; }
    public HttpClientConfig HttpClientConfig { get; set; }
    public ForwarderRequestConfig HttpRequest { get; set; }
    public string LoadBalancingPolicy { get; set; }
    public string CorsPolicy { get; set; }
    public string TimeoutPolicy { get; set; }
    public TimeSpan? Timeout { get; set; }
    public HealthCheckConfig HealthCheck { get; set; }
    public Dictionary<string, string> RouteMetadata { get; set; }
    public List<RouteHeader> RouteHeaders { get; set; }
    public List<RouteQueryParameter> RouteQueryParameters { get; set; }
    public int? RouteOrder { get; set; }
    public List<string> RouteMethods { get; set; }
}

internal sealed class RouteHeaderWrapper
{
    public string Name { get; init; }
    public List<string> Values { get; init; }
    public HeaderMatchMode Mode { get; init; }
    public bool IsCaseSensitive { get; init; }

    public RouteHeader ToRouteHeader()
    {
        return new RouteHeader
        {
            Name = Name,
            Values = Values,
            Mode = Mode,
            IsCaseSensitive = IsCaseSensitive
        };
    }
}

internal sealed class RouteQueryParameterWrapper
{
    public string Name { get; init; }
    public List<string> Values { get; init; }
    public QueryParameterMatchMode Mode { get; init; }
    public bool IsCaseSensitive { get; init; }

    public RouteQueryParameter ToRouteQueryParameter()
    {
        return new RouteQueryParameter
        {
            Name = Name,
            Values = Values,
            Mode = Mode,
            IsCaseSensitive = IsCaseSensitive
        };
    }
}
