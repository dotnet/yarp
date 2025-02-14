// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Defines options for the transport failure rate passive health policy.
/// </summary>
public class TransportFailureRateHealthPolicyOptions
{
    /// <summary>
    /// Name of failure rate limit metadata parameter. Destination marked as unhealthy once this limit is reached.
    /// </summary>
    public static readonly string FailureRateLimitMetadataName = "TransportFailureRateHealthPolicy.RateLimit";

    /// <summary>
    /// Period of time while detected failures are kept and taken into account in the rate calculation.
    /// The default is 60 seconds.
    /// </summary>
    public TimeSpan DetectionWindowSize { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Minimal total number of requests which must be proxied to a destination within the detection window
    /// before this policy starts evaluating the destination's health and enforcing the failure rate limit.
    /// The default is 10.
    /// </summary>
    public int MinimalTotalCountThreshold { get; set; } = 10;

    /// <summary>
    /// Default failure rate limit for a destination to be marked as unhealthy that is applied if it's not set on a cluster's metadata.
    /// It's calculated as a percentage of failed requests out of all requests proxied to the same destination in the <see cref="DetectionWindowSize"/> period.
    /// The value is in range (0,1). The default is 0.3 (30%).
    /// </summary>
    public double DefaultFailureRateLimit { get; set; } = 0.3;
}
