// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Represents metrics reported by the System.Net.NameResolution event counters.
/// </summary>
public sealed class NameResolutionMetrics
{
    public NameResolutionMetrics() => Timestamp = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of when this <see cref="NameResolutionMetrics"/> instance was created.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Number of DNS lookups requested since telemetry was enabled.
    /// </summary>
    public long DnsLookupsRequested { get; internal set; }

    /// <summary>
    /// Average DNS lookup duration in the last metrics interval.
    /// </summary>
    public TimeSpan AverageLookupDuration { get; internal set; }

    /// <summary>
    /// Number of DNS lookups that have started but not yet completed or failed.
    /// </summary>
    public long CurrentDnsLookups { get; internal set; }
}
