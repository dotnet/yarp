// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Represents metrics reported by the System.Net.Sockets event counters.
/// </summary>
public sealed class SocketsMetrics
{
    public SocketsMetrics() => Timestamp = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of when this <see cref="SocketsMetrics"/> instance was created.
    /// </summary>
    public DateTime Timestamp { get; internal set; }

    /// <summary>
    /// Number of outgoing (Connect) Socket connections established since telemetry was enabled.
    /// </summary>
    public long OutgoingConnectionsEstablished { get; internal set; }

    /// <summary>
    /// Number of incoming (Accept) Socket connections established since telemetry was enabled.
    /// </summary>
    public long IncomingConnectionsEstablished { get; internal set; }

    /// <summary>
    /// Number of bytes received since telemetry was enabled.
    /// </summary>
    public long BytesReceived { get; internal set; }

    /// <summary>
    /// Number of bytes sent since telemetry was enabled.
    /// </summary>
    public long BytesSent { get; internal set; }

    /// <summary>
    /// Number of datagrams received since telemetry was enabled.
    /// </summary>
    public long DatagramsReceived { get; internal set; }

    /// <summary>
    /// Number of datagrams sent since telemetry was enabled.
    /// </summary>
    public long DatagramsSent { get; internal set; }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Number of outgoing (Connect) Socket connection attempts that are currently in progress.
    /// </summary>
    public long CurrentOutgoingConnectAttempts { get; internal set; }
#endif
}
