// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Sockets;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestClusterHostBuilder
{
    private readonly IHostBuilder _hostBuilder = new HostBuilder();

    public ITestClusterHost Build()
    {
        if (string.IsNullOrEmpty(ServerUrl))
        {
            ServerUrl = $"http://{IPAddress.Loopback}:{AvailablePort()}";
        }

        _hostBuilder.ConfigureWebHostDefaults(web =>
        {
            web
                .UseStartup<TestClusterStartup>()
                .UseUrls(ServerUrl);
        });

        var host = _hostBuilder.Build();

        var kubeConfig = new K8SConfiguration
        {
            ApiVersion = "v1",
            Kind = "Config",
            CurrentContext = "test-context",
            Contexts = new[]
            {
                new Context
                {
                    Name = "test-context",
                    ContextDetails = new ContextDetails
                    {
                        Namespace = "test-namespace",
                        Cluster = "test-cluster",
                        User = "test-user",
                    }
                }
            },
            Clusters = new[]
            {
                new Cluster
                {
                    Name = "test-cluster",
                    ClusterEndpoint = new ClusterEndpoint
                    {
                        Server = ServerUrl,
                    }
                }
            },
            Users = new[]
            {
                new User
                {
                    Name = "test-user",
                    UserCredentials = new UserCredentials
                    {
                        Token = "test-token",
                    }
                }
            },
        };

        var clientConfiguration = KubernetesClientConfiguration.BuildConfigFromConfigObject(kubeConfig);

        var client = new k8s.Kubernetes(clientConfiguration);

        return new TestClusterHost(host, kubeConfig, client);
    }

    public TestClusterHostBuilder UseInitialResources(params IKubernetesObject<V1ObjectMeta>[] resources)
    {
        return ConfigureServices(services =>
        {
            services.Configure<TestClusterOptions>(options =>
            {
                foreach (var resource in resources)
                {
                    options.InitialResources.Add(resource);
                }
            });
        });
    }

    public TestClusterHostBuilder ConfigureServices(Action<IServiceCollection> configureDelegate)
    {
        _hostBuilder.ConfigureServices(configureDelegate);
        return this;
    }

    public TestClusterHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        _hostBuilder.ConfigureServices(configureDelegate);
        return this;
    }

    public TestClusterHostBuilder Configure(Action<TestClusterOptions> configureOptions)
    {
        _hostBuilder.ConfigureServices(services => Configure(configureOptions));
        return this;
    }

    private static int AvailablePort()
    {
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return ((IPEndPoint)socket.LocalEndPoint).Port;
    }

    public string ServerUrl { get; set; }
}
