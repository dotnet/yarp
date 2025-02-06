// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.Kubernetes.Controller.Management.Tests;

public class KubernetesCoreExtensionsTests
{
    [Fact]
    public void KubernetesClientIsAdded()
    {
        var services = new ServiceCollection();

        services.AddKubernetesCore();

        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IKubernetes>());
    }

    [Fact]
    public void ExistingClientIsNotReplaced()
    {
        using var client = new k8s.Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        var services = new ServiceCollection();

        services.AddSingleton<IKubernetes>(client);
        services.AddKubernetesCore();

        var serviceProvider = services.BuildServiceProvider();
        Assert.Same(client, serviceProvider.GetService<IKubernetes>());
    }
}
