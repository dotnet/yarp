// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal static class DelegationExtensions
{
    public const string HttpSysDelegationQueueMetadataKey = "HttpSysDelegationQueue";

    public static string? GetHttpSysDelegationQueue(this DestinationState? destination)
    {
        return destination?.Model?.Config?.Metadata?.TryGetValue(HttpSysDelegationQueueMetadataKey, out var name) ?? false
            ? name
            : null;
    }

    public static bool ShouldUseHttpSysDelegation(this DestinationState destination)
    {
        return destination.GetHttpSysDelegationQueue() is not null;
    }
}
