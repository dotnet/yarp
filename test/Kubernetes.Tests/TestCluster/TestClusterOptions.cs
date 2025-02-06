// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using System.Collections.Generic;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestClusterOptions
{
    public IList<IKubernetesObject<V1ObjectMeta>> InitialResources { get; } = new List<IKubernetesObject<V1ObjectMeta>>();
}
