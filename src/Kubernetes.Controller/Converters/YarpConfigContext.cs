// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Converters;

internal class YarpConfigContext
{
    public Dictionary<string, ClusterTransfer> ClusterTransfers { get; set; } = new Dictionary<string, ClusterTransfer>();
    public List<RouteConfig> Routes { get; set; } = new List<RouteConfig>();

    public List<ClusterConfig> BuildClusterConfig()
    {
        return ClusterTransfers.Values.Select(c => new ClusterConfig()
        {
            Destinations = c.Destinations,
            ClusterId = c.ClusterId,
            HealthCheck = c.HealthCheck,
            LoadBalancingPolicy = c.LoadBalancingPolicy,
            SessionAffinity = c.SessionAffinity,
            HttpClient = c.HttpClientConfig,
            HttpRequest = c.HttpRequest
        }).ToList();
    }
}
