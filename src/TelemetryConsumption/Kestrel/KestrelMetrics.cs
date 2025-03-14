// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Represents metrics reported by the Microsoft-AspNetCore-Server-Kestrel event counters.
/// </summary>
public sealed class KestrelMetrics
{
    public KestrelMetrics() => Timestamp = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of when this <see cref="KestrelMetrics"/> instance was created.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Number of connections opened in the last metrics interval.
    /// </summary>
    public long ConnectionRate { get; internal set; }

    /// <summary>
    /// Number of connections opened since telemetry was enabled.
    /// </summary>
    public long TotalConnections { get; internal set; }

    /// <summary>
    /// Number of TLS handshakes started in the last metrics interval.
    /// </summary>
    public long TlsHandshakeRate { get; internal set; }

    /// <summary>
    /// Number of TLS handshakes started since telemetry was enabled.
    /// </summary>
    public long TotalTlsHandshakes { get; internal set; }

    /// <summary>
    /// Number of active TLS handshakes that have started but not yet completed or failed.
    /// </summary>
    public long CurrentTlsHandshakes { get; internal set; }

    /// <summary>
    /// Number of TLS handshakes that failed since telemetry was enabled.
    /// </summary>
    public long FailedTlsHandshakes { get; internal set; }

    /// <summary>
    /// Number of currently open connections.
    /// </summary>
    public long CurrentConnections { get; internal set; }

    /// <summary>
    /// Number of connections on the queue.
    /// </summary>
    public long ConnectionQueueLength { get; internal set; }

    /// <summary>
    /// Number of requests on the queue.
    /// </summary>
    public long RequestQueueLength { get; internal set; }

    /// <summary>
    /// Number of currently upgraded requests (number of webSocket connections).
    /// </summary>
    public long CurrentUpgradedRequests { get; internal set; }
}
