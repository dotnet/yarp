// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of Yarp.ReverseProxy.WebSockets EventSource events.
/// </summary>
public interface IWebSocketsTelemetryConsumer
{
    /// <summary>
    /// Called when a WebSockets connection is closed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="establishedTime">Timestamp when the connection upgrade completed.</param>
    /// <param name="closeReason">The reason the WebSocket connection closed.</param>
    /// <param name="messagesRead">Messages read by the destination server.</param>
    /// <param name="messagesWritten">Messages sent by the destination server.</param>
    void OnWebSocketClosed(DateTime timestamp, DateTime establishedTime, WebSocketCloseReason closeReason, long messagesRead, long messagesWritten);
}
