// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Client.Tests;
using Yarp.Kubernetes.Controller.Services;
using Yarp.Kubernetes.Tests.Utils;
using Yarp.Tests.Common;

namespace Yarp.Kubernetes.Tests;

public class IngressControllerTests
{
    private readonly Mock<ICache> _mockCache = new();
    private readonly Mock<IReconciler> _mockReconciler = new();
    private readonly SyncResourceInformer<V1Ingress> _ingressInformer = new();
    private readonly SyncResourceInformer<V1Service> _serviceInformer = new();
    private readonly SyncResourceInformer<V1Endpoints> _endpointsInformer = new();
    private readonly SyncResourceInformer<V1IngressClass> _ingressClassInformer = new();
    private readonly SyncResourceInformer<V1Secret> _secretInformer = new();
    private readonly Mock<IHostApplicationLifetime> _mockHostApplicationLifetime = new();
    private readonly Mock<IOptions<YarpOptions>> _mockOptions = new();
    private readonly IngressController _controller;

    public IngressControllerTests(ITestOutputHelper output)
    {
        var optionsNoWatchSecrets = new YarpOptions()
        {
            ServerCertificates = false,
        };

        _mockOptions.Setup(o => o.Value).Returns(optionsNoWatchSecrets);

        var logger = new TestLogger<IngressController>(output);
        _controller = new IngressController(_mockCache.Object, _mockReconciler.Object, _ingressInformer, _serviceInformer, _endpointsInformer, _ingressClassInformer, _secretInformer, _mockHostApplicationLifetime.Object, _mockOptions.Object, logger);
    }

    [Fact]
    public async Task ReconciliationContinuesOnReconcilerError()
    {
        _mockCache.Setup(x => x.Update(It.IsAny<WatchEventType>(), It.IsAny<V1Ingress>())).Returns(true);

        var awaiter = new SemaphoreSlim(0, 1);
        _mockReconciler
            .SetupSequence(x => x.ProcessAsync(It.IsAny<CancellationToken>()))
                .Returns(() => { awaiter.Release(); return Task.CompletedTask; })
                .Returns(() => { awaiter.Release(); return Task.CompletedTask; })
                .Returns(() => { awaiter.Release(); return Task.FromException(new Exception("reconicliation failed")); })
                .Returns(() => { awaiter.Release(); return Task.CompletedTask; });

        await _controller.StartAsync(CancellationToken.None).DefaultTimeout();
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(1));

        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(4));
    }
}
