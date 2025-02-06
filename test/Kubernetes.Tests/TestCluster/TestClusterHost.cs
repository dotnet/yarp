// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestClusterHost : ITestClusterHost
{
    private readonly IHost _host;
    private bool _disposedValue;

    public TestClusterHost(IHost host, K8SConfiguration kubeConfig, IKubernetes client)
    {
        _host = host;
        KubeConfig = kubeConfig;
        Client = client;
    }

    public IServiceProvider Services => _host.Services;

    public ITestCluster Cluster => _host.Services.GetRequiredService<ITestCluster>();

    public K8SConfiguration KubeConfig { get; }

    public IKubernetes Client { get; }

    public Task StartAsync(CancellationToken cancellationToken = default) => _host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _host.StartAsync(cancellationToken);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _host.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
