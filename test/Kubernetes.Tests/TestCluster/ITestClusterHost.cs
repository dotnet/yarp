// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Hosting;

namespace Yarp.Kubernetes.Tests.TestCluster;

public interface ITestClusterHost : IHost
{
    K8SConfiguration KubeConfig { get; }

    IKubernetes Client { get; }

    ITestCluster Cluster { get; }
}
