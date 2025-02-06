// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class RedistributeAffinityFailurePolicyTests
{
    [Theory]
    [InlineData(AffinityStatus.AffinityKeyExtractionFailed)]
    [InlineData(AffinityStatus.DestinationNotFound)]
    public async Task Handle_FailedAffinityStatus_ReturnTrue(AffinityStatus status)
    {
        var policy = new RedistributeAffinityFailurePolicy();

        Assert.Equal(SessionAffinityConstants.FailurePolicies.Redistribute, policy.Name);
        Assert.True(await policy.Handle(new DefaultHttpContext(), cluster: null, affinityStatus: status));
    }

    [Theory]
    [InlineData(AffinityStatus.OK)]
    [InlineData(AffinityStatus.AffinityKeyNotSet)]
    public async Task Handle_SuccessfulAffinityStatus_Throw(AffinityStatus status)
    {
        var policy = new RedistributeAffinityFailurePolicy();
        var context = new DefaultHttpContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.Handle(context, cluster: null, affinityStatus: status));
    }
}
