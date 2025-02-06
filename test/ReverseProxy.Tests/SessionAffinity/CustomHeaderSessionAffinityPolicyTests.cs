// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class CustomHeaderSessionAffinityPolicyTests
{
    private const string AffinityHeaderName = "X-MyAffinity";
    private readonly SessionAffinityConfig _defaultOptions = new SessionAffinityConfig
    {
        Enabled = true,
        Policy = "Cookie",
        FailurePolicy = "Return503Error",
        AffinityKeyName = AffinityHeaderName
    };
    private readonly IReadOnlyList<DestinationState> _destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
    {
        var policy = new CustomHeaderSessionAffinityPolicy(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityPolicy>().Object);

        Assert.Equal(SessionAffinityConstants.Policies.CustomHeader, policy.Name);

        var context = new DefaultHttpContext();
        context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _defaultOptions, _destinations);

        Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
        Assert.Null(affinityResult.Destinations);
    }

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
    {
        var policy = new CustomHeaderSessionAffinityPolicy(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityPolicy>().Object);
        var context = new DefaultHttpContext();
        context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
        var affinitizedDestination = _destinations[1];
        context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _defaultOptions, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);
        Assert.Single(affinityResult.Destinations);
        Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
    }

    [Fact]
    public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
    {
        var policy = new CustomHeaderSessionAffinityPolicy(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityPolicy>().Object);
        var context = new DefaultHttpContext();
        var chosenDestination = _destinations[1];
        var expectedAffinityHeaderValue = chosenDestination.DestinationId.ToUTF8BytesInBase64();

        policy.AffinitizeResponse(context, new ClusterState("cluster"), _defaultOptions, chosenDestination);

        Assert.True(context.Response.Headers.ContainsKey(AffinityHeaderName));
        Assert.Equal(expectedAffinityHeaderValue, context.Response.Headers[AffinityHeaderName]);
    }

    [Fact]
    public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
    {
        var policy = new CustomHeaderSessionAffinityPolicy(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityPolicy>().Object);
        var context = new DefaultHttpContext();
        context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
        var affinitizedDestination = _destinations[1];
        context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _defaultOptions, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);

        policy.AffinitizeResponse(context, cluster, _defaultOptions, affinitizedDestination);

        Assert.False(context.Response.Headers.ContainsKey(AffinityHeaderName));
    }
}
