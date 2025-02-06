// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Represents metrics reported by the Yarp.ReverseProxy event counters.
/// </summary>
public sealed class ForwarderMetrics
{
    public ForwarderMetrics() => Timestamp = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of when this <see cref="ForwarderMetrics"/> instance was created.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Number of proxy requests started since telemetry was enabled.
    /// </summary>
    public long RequestsStarted { get; internal set; }

    /// <summary>
    /// Number of proxy requests started in the last metrics interval.
    /// </summary>
    public long RequestsStartedRate { get; internal set; }

    /// <summary>
    /// Number of proxy requests that failed since telemetry was enabled.
    /// </summary>
    public long RequestsFailed { get; internal set; }

    /// <summary>
    /// Number of active proxy requests that have started but not yet completed or failed.
    /// </summary>
    public long CurrentRequests { get; internal set; }
}
