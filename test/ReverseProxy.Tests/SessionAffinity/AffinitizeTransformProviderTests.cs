// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class AffinitizeTransformProviderTests
{
    [Fact]
    public void EnableSessionAffinity_AddsTransform()
    {
        var affinityPolicy = new Mock<ISessionAffinityPolicy>(MockBehavior.Strict);
        affinityPolicy.SetupGet(p => p.Name).Returns("Policy");

        var transformProvider = new AffinitizeTransformProvider(new[] { affinityPolicy.Object });

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                Policy = "Policy",
                AffinityKeyName = "Key1"
            }
        };

        var validationContext = new TransformClusterValidationContext()
        {
            Cluster = cluster,
        };
        transformProvider.ValidateCluster(validationContext);

        Assert.Empty(validationContext.Errors);

        var builderContext = new TransformBuilderContext()
        {
            Cluster = cluster,
        };
        transformProvider.Apply(builderContext);

        Assert.IsType<AffinitizeTransform>(builderContext.ResponseTransforms.Single());
    }

    [Fact]
    public void EnableSession_InvalidMode_Fails()
    {
        var affinityPolicy = new Mock<ISessionAffinityPolicy>(MockBehavior.Strict);
        affinityPolicy.SetupGet(p => p.Name).Returns("Policy");

        var transformProvider = new AffinitizeTransformProvider(new[] { affinityPolicy.Object });

        var cluster = new ClusterConfig
        {
            ClusterId = "cluster1",
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                Policy = "Invalid",
                AffinityKeyName = "Key1"
            }
        };

        var validationContext = new TransformClusterValidationContext()
        {
            Cluster = cluster,
        };
        transformProvider.ValidateCluster(validationContext);

        var ex = Assert.Single(validationContext.Errors);
        Assert.Equal("No matching ISessionAffinityPolicy found for the session affinity policy 'Invalid' set on the cluster 'cluster1'.", ex.Message);

        var builderContext = new TransformBuilderContext()
        {
            Cluster = cluster,
        };

        ex = Assert.Throws<ArgumentException>(() => transformProvider.Apply(builderContext));
        Assert.Equal($"No {typeof(ISessionAffinityPolicy).FullName} was found for the id 'Invalid'. (Parameter 'id')", ex.Message);
    }
}
