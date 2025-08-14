// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Client;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.Kubernetes.Controller.Services;
using Yarp.Kubernetes.Tests.Utils;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Tests;

public class ReconcilerTests
{
    private readonly Mock<ICache> _mockCache = new();
    private readonly Mock<IUpdateConfig> _mockUpdateConfig = new();
    private readonly Mock<IIngressResourceStatusUpdater> _mockIngressResourceStatusUpdater = new();
    private readonly Reconciler _reconciler;

    public ReconcilerTests(ITestOutputHelper output)
    {
        var logger = new TestLogger<Reconciler>(output);
        _reconciler = new Reconciler(_mockCache.Object, _mockUpdateConfig.Object, _mockIngressResourceStatusUpdater.Object, logger);
    }

    [Fact]
    public async Task ReconcilerDoesNotStopOnInvalidIngress()
    {
        _mockCache
            .Setup(x => x.GetIngresses())
            .Returns(new[]
                {
                    new IngressData(KubeResourceGenerator.CreateIngress("bad-ingress", "default", "yarp")),
                    new IngressData(KubeResourceGenerator.CreateIngress("good-ingress", "default", "yarp"))
                });

        _mockCache
            .Setup(x => x.TryGetReconcileData(It.IsAny<NamespacedName>(), out It.Ref<ReconcileData>.IsAny))
            .Returns(true);

        _mockCache
            .Setup(x => x.TryGetReconcileData(new NamespacedName("default", "bad-ingress"), out It.Ref<ReconcileData>.IsAny))
            .Throws(new Exception("poison ingress"));

        await _reconciler.ProcessAsync(CancellationToken.None);
        _mockUpdateConfig.Verify(x => x.UpdateAsync(It.IsAny<IReadOnlyList<RouteConfig>>(), It.IsAny<IReadOnlyList<ClusterConfig>>(), It.IsAny<CancellationToken>()));
    }
}
