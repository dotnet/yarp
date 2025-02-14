// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class Return503ErrorAffinityFailurePolicyTests
{
    [Theory]
    [InlineData(AffinityStatus.DestinationNotFound)]
    [InlineData(AffinityStatus.AffinityKeyExtractionFailed)]
    public async Task Handle_FaultyAffinityStatus_RespondWith503(AffinityStatus status)
    {
        var policy = new Return503ErrorAffinityFailurePolicy();
        var context = new DefaultHttpContext();

        Assert.Equal(SessionAffinityConstants.FailurePolicies.Return503Error, policy.Name);

        Assert.False(await policy.Handle(context, cluster: null, affinityStatus: status));
        Assert.Equal(503, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(AffinityStatus.OK)]
    [InlineData(AffinityStatus.AffinityKeyNotSet)]
    public async Task Handle_SuccessfulAffinityStatus_Throw(AffinityStatus status)
    {
        var policy = new Return503ErrorAffinityFailurePolicy();
        var context = new DefaultHttpContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.Handle(context, cluster: null, affinityStatus: status));
    }
}
