// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class ArrCookieSessionAffinityPolicyTests
{
    private readonly SessionAffinityConfig _config = new()
    {
        Enabled = true,
        Policy = "ArrCookie",
        FailurePolicy = "Return503Error",
        AffinityKeyName = "My.Affinity",
        Cookie = new SessionAffinityCookieConfig
        {
            Domain = "mydomain.my",
            HttpOnly = false,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(1),
            Path = "/some",
            SameSite = SameSiteMode.Lax,
            SecurePolicy = CookieSecurePolicy.Always,
        }
    };
    private readonly IReadOnlyList<DestinationState> _destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
    {
        var policy = new ArrCookieSessionAffinityPolicy(
            new TestTimeProvider(),
            NullLogger<ArrCookieSessionAffinityPolicy>.Instance);

        Assert.Equal(SessionAffinityConstants.Policies.ArrCookie, policy.Name);

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = new[] { $"Some-Cookie=ZZZ" };
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
        Assert.Null(affinityResult.Destinations);
    }

    [Fact]
    public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
    {
        var policy = new ArrCookieSessionAffinityPolicy(
            new TestTimeProvider(),
            NullLogger<ArrCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();
        var affinitizedDestination = _destinations[1];
        context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);
        Assert.Single(affinityResult.Destinations);
        Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
    }

    [Fact]
    public void AffinitizedRequest_CustomConfigAffinityKeyIsNotExtracted_SetKeyOnResponse()
    {
        var policy = new ArrCookieSessionAffinityPolicy(
            new TestTimeProvider(),
            NullLogger<ArrCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();

        policy.AffinitizeResponse(context, new ClusterState("cluster"), _config, _destinations[1]);

        var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
        Assert.Equal("My.Affinity=920A160FA519353932B655488361A944531650016793761EE7224DE632863B13; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax",
            affinityCookieHeader);
    }

    [Fact]
    public void AffinitizeRequest_CookieConfigSpecified_UseIt()
    {
        var policy = new ArrCookieSessionAffinityPolicy(
            new TestTimeProvider(),
            NullLogger<ArrCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();

        policy.AffinitizeResponse(context, new ClusterState("cluster"), _config, _destinations[1]);

        var affinityCookieHeader = context.Response.Headers["Set-Cookie"];
        Assert.Equal("My.Affinity=920A160FA519353932B655488361A944531650016793761EE7224DE632863B13; max-age=3600; domain=mydomain.my; path=/some; secure; samesite=lax",
            affinityCookieHeader);
    }

    [Fact]
    public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
    {
        var policy = new ArrCookieSessionAffinityPolicy(
            new TestTimeProvider(),
            NullLogger<ArrCookieSessionAffinityPolicy>.Instance);
        var context = new DefaultHttpContext();
        var affinitizedDestination = _destinations[0];
        context.Request.Headers["Cookie"] = GetCookieWithAffinity(affinitizedDestination);
        var cluster = new ClusterState("cluster");

        var affinityResult = policy.FindAffinitizedDestinations(context, cluster, _config, _destinations);

        Assert.Equal(AffinityStatus.OK, affinityResult.Status);

        policy.AffinitizeResponse(context, cluster, _config, affinitizedDestination);

        Assert.False(context.Response.Headers.ContainsKey("Cookie"));
    }

    private string[] GetCookieWithAffinity(DestinationState affinitizedDestination)
    {
        var destinationIdBytes = Encoding.Unicode.GetBytes(affinitizedDestination.DestinationId.ToLowerInvariant());
        var hashBytes = SHA256.HashData(destinationIdBytes);
        var value = Convert.ToHexString(hashBytes);
        return new[] { $"Some-Cookie=ZZZ", $"{_config.AffinityKeyName}={value}" };
    }
}
