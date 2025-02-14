// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class Return503ErrorAffinityFailurePolicy : IAffinityFailurePolicy
{
    public string Name => SessionAffinityConstants.FailurePolicies.Return503Error;

    public Task<bool> Handle(HttpContext context, ClusterState cluster, AffinityStatus affinityStatus)
    {
        if (affinityStatus == AffinityStatus.OK
            || affinityStatus == AffinityStatus.AffinityKeyNotSet)
        {
            throw new InvalidOperationException($"{nameof(Return503ErrorAffinityFailurePolicy)} is called to handle a successful request's affinity status {affinityStatus}.");
        }

        context.Response.StatusCode = 503;
        return TaskUtilities.FalseTask;
    }
}
