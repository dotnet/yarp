// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Kubernetes.Tests.TestCluster;
using Yarp.Kubernetes.Tests.Utils;

namespace Yarp.Kubernetes.Controller.Client.Tests;

public class ResourceInformerTests
{
    private static (TResources Resources, TShouldBe ShouldBe) LoadTestResource<TResources, TShouldBe>(string name)
    {
        var resourcesYaml = File.ReadAllText(Path.Combine("testassets/resource-informer", name, "resources.yaml"));
        var shouldBeYaml = File.ReadAllText(Path.Combine("testassets/resource-informer", name, "shouldbe.yaml"));

        var resources = ResourceSerializers.DeserializeYaml<TResources>(resourcesYaml);
        var shouldBe = ResourceSerializers.DeserializeYaml<TShouldBe>(shouldBeYaml);

        return (resources, shouldBe);
    }

    [Fact]
    public async Task ResourcesAreListedWhenReadyAsyncIsComplete()
    {
        using var cancellation = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(5));

        var (resources, shouldBe) = LoadTestResource<V1Pod[], NamespacedName[]>(nameof(ResourcesAreListedWhenReadyAsyncIsComplete));

        using var clusterHost = new TestClusterHostBuilder()
            .UseInitialResources(resources)
            .Build();

        using var testHost = new HostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddKubernetesReverseProxy(context.Configuration);
                services.RegisterResourceInformer<V1Pod, V1PodResourceInformer>();
                services.Configure<KubernetesClientOptions>(options =>
                {
                    options.Configuration = KubernetesClientConfiguration.BuildConfigFromConfigObject(clusterHost.KubeConfig);
                });
            })
            .Build();

        var informer = testHost.Services.GetRequiredService<IResourceInformer<V1Pod>>();
        var pods = new Dictionary<NamespacedName, V1Pod>();

        informer.StartWatching();
        using var registration = informer.Register((eventType, pod) =>
        {
            pods[NamespacedName.From(pod)] = pod;
        });

        await clusterHost.StartAsync(cancellation.Token);
        await testHost.StartAsync(cancellation.Token);

        await registration.ReadyAsync(cancellation.Token);

        Assert.Equal(shouldBe, pods.Keys);
    }

    [Fact]
    public async Task ResourcesWithApiGroupAreListed()
    {
        using var cancellation = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(5));

        var (resources, shouldBe) = LoadTestResource<V1Deployment[], NamespacedName[]>(nameof(ResourcesWithApiGroupAreListed));

        using var clusterHost = new TestClusterHostBuilder()
            .UseInitialResources(resources)
            .Build();

        using var testHost = new HostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddKubernetesReverseProxy(context.Configuration);
                services.RegisterResourceInformer<V1Deployment, V1DeploymentResourceInformer>();
                services.Configure<KubernetesClientOptions>(options =>
                {
                    options.Configuration = KubernetesClientConfiguration.BuildConfigFromConfigObject(clusterHost.KubeConfig);
                });
            })
            .Build();

        var informer = testHost.Services.GetRequiredService<IResourceInformer<V1Deployment>>();
        var deployments = new Dictionary<NamespacedName, V1Deployment>();

        informer.StartWatching();
        using var registration = informer.Register((eventType, deployment) =>
        {
            deployments[NamespacedName.From(deployment)] = deployment;
        });

        await clusterHost.StartAsync(cancellation.Token);
        await testHost.StartAsync(cancellation.Token);

        await registration.ReadyAsync(cancellation.Token);

        Assert.Equal(shouldBe, deployments.Keys);
    }

    [Fact]
    public Task WatchIsRestartedWhenConnectionClosesBeforeFirstEvent()
    {
        return AssertWatchIsRestartedAsync(
            new HttpRequestException("Error while copying content to a stream.", new EndOfStreamException()),
            failAfterFirstEvent: false);
    }

    [Fact]
    public Task WatchIsRestartedWhenServerClosesConnectionMidStream()
    {
        return AssertWatchIsRestartedAsync(
            new HttpProtocolException(0, "The HTTP/2 server closed the connection. HTTP/2 error code 'NO_ERROR' (0x0).", null),
            failAfterFirstEvent: true);
    }

    private static async Task AssertWatchIsRestartedAsync(Exception firstWatchFailure, bool failAfterFirstEvent)
    {
        using var cancellation = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(5));
        using var stopping = new CancellationTokenSource();

        var hostApplicationLifetime = new Mock<IHostApplicationLifetime>();
        hostApplicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(stopping.Token);

        using var informer = new FlakyWatchResourceInformer(hostApplicationLifetime.Object, firstWatchFailure, failAfterFirstEvent);

        informer.StartWatching();
        await informer.StartAsync(cancellation.Token);

        await informer.WatchRestarted.Task.WaitAsync(cancellation.Token);

        hostApplicationLifetime.Verify(lifetime => lifetime.StopApplication(), Times.Never);

        stopping.Cancel();
        await informer.StopAsync(cancellation.Token);
    }
}
