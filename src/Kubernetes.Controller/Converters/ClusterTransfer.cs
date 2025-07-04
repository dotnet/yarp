// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Kubernetes.Controller.Converters;

internal sealed class ClusterTransfer
{
    public Dictionary<string, DestinationConfig> Destinations { get; set; } = new Dictionary<string, DestinationConfig>();
    public string ClusterId { get; set; }
    public string LoadBalancingPolicy { get; set; }
    public SessionAffinityConfig SessionAffinity { get; set; }
    public HealthCheckConfig HealthCheck { get; set; }
    public HttpClientConfig HttpClientConfig { get; set; }
    public ForwarderRequestConfig HttpRequest { get; set; }
}
