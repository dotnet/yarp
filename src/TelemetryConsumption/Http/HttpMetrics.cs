// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Represents metrics reported by the System.Net.Http event counters.
/// </summary>
public sealed class HttpMetrics
{
    public HttpMetrics() => Timestamp = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of when this <see cref="KestrelMetrics"/> instance was created.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Number of HTTP requests started since telemetry was enabled.
    /// </summary>
    public long RequestsStarted { get; internal set; }

    /// <summary>
    /// Number of HTTP requests started in the last metrics interval.
    /// </summary>
    public long RequestsStartedRate { get; internal set; }

    /// <summary>
    /// Number of HTTP requests that failed since telemetry was enabled.
    /// </summary>
    public long RequestsFailed { get; internal set; }

    /// <summary>
    /// Number of HTTP requests that failed in the last metrics interval.
    /// </summary>
    public long RequestsFailedRate { get; internal set; }

    /// <summary>
    /// Number of active HTTP requests that have started but not yet completed or failed.
    /// </summary>
    public long CurrentRequests { get; internal set; }

    /// <summary>
    /// Number of currently open HTTP 1.1 connections.
    /// </summary>
    public long CurrentHttp11Connections { get; internal set; }

    /// <summary>
    /// Number of currently open HTTP 2.0 connections.
    /// </summary>
    public long CurrentHttp20Connections { get; internal set; }

    /// <summary>
    /// Average time spent on queue for HTTP 1.1 requests that hit the MaxConnectionsPerServer limit in the last metrics interval.
    /// </summary>
    public TimeSpan Http11RequestsQueueDuration { get; internal set; }

    /// <summary>
    /// Average time spent on queue for HTTP 2.0 requests that hit the MAX_CONCURRENT_STREAMS limit on the connection in the last metrics interval.
    /// </summary>
    public TimeSpan Http20RequestsQueueDuration { get; internal set; }

    /// <summary>
    /// Number of currently open HTTP 3.0 connections.
    /// </summary>
    public long CurrentHttp30Connections { get; internal set; }

    /// <summary>
    /// Average time spent on queue for HTTP 3.0 requests in the last metrics interval.
    /// </summary>
    public TimeSpan Http30RequestsQueueDuration { get; internal set; }
}
