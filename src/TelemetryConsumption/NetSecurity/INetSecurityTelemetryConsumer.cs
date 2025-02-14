// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of System.Net.Security EventSource events.
/// </summary>
public interface INetSecurityTelemetryConsumer
{
    /// <summary>
    /// Called before a handshake.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="isServer">Indicates whether we are authenticating as the server.</param>
    /// <param name="targetHost">Name of the host we are authenticating with.</param>
    void OnHandshakeStart(DateTime timestamp, bool isServer, string targetHost) { }

    /// <summary>
    /// Called after a handshake.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="protocol">The protocol established by the handshake.</param>
    void OnHandshakeStop(DateTime timestamp, SslProtocols protocol) { }

    /// <summary>
    /// Called before <see cref="OnHandshakeStop(DateTime, SslProtocols)"/> if the handshake failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="isServer">Indicates whether we were authenticating as the server.</param>
    /// <param name="elapsed">Time elapsed since the start of the handshake.</param>
    /// <param name="exceptionMessage">Exception information for the handshake failure.</param>
    void OnHandshakeFailed(DateTime timestamp, bool isServer, TimeSpan elapsed, string exceptionMessage) { }
}
