// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Sockets;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of System.Net.Sockets EventSource events.
/// </summary>
public interface ISocketsTelemetryConsumer
{
    /// <summary>
    /// Called before a Socket connect.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="address">Socket address we are connecting to.</param>
    void OnConnectStart(DateTime timestamp, string address) { }

    /// <summary>
    /// Called after a Socket connect.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnConnectStop(DateTime timestamp) { }

    /// <summary>
    /// Called before <see cref="OnConnectStop(DateTime)"/> if the connect failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="error"><see cref="SocketError"/> information for the connect failure.</param>
    /// <param name="exceptionMessage">Exception information for the connect failure.</param>
    void OnConnectFailed(DateTime timestamp, SocketError error, string exceptionMessage) { }
}
