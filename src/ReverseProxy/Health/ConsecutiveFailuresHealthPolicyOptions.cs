// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Defines options for the consecutive failures active health check policy.
/// </summary>
public class ConsecutiveFailuresHealthPolicyOptions
{
    /// <summary>
    /// Name of the consecutive failure threshold metadata parameter.
    /// It's the number of consecutive failure that needs to happen in order to mark a destination as unhealthy.
    /// </summary>
    public static readonly string ThresholdMetadataName = "ConsecutiveFailuresHealthPolicy.Threshold";

    /// <summary>
    /// Default consecutive failures threshold that is applied if it's not set on a cluster's metadata.
    /// </summary>
    public long DefaultThreshold { get; set; } = 2;
}
