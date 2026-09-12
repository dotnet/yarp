// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Yarp.Kubernetes.Protocol;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller.Protocol;

[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(List<RouteConfig>))]
[JsonSerializable(typeof(List<ClusterConfig>))]
internal sealed partial class KubernetesJsonSerializerContext : JsonSerializerContext
{
}
