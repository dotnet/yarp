// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of Microsoft-AspNetCore-Server-Kestrel EventSource events.
/// </summary>
public interface IKestrelTelemetryConsumer
{
    /// <summary>
    /// Called at the start of a connection.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="connectionId">ID of the connection.</param>
    /// <param name="localEndPoint">Local endpoint for the connection.</param>
    /// <param name="remoteEndPoint">Remote endpoint for the connection.</param>
    void OnConnectionStart(DateTime timestamp, string connectionId, string? localEndPoint, string? remoteEndPoint) { }

    /// <summary>
    /// Called at the end of a connection.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="connectionId">ID of the connection.</param>
    void OnConnectionStop(DateTime timestamp, string connectionId) { }

    /// <summary>
    /// Called at the start of a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="connectionId">ID of the connection.</param>
    /// <param name="requestId">ID of the request.</param>
    /// <param name="httpVersion">HTTP version of the request.</param>
    /// <param name="path">Path of the request.</param>
    /// <param name="method">HTTP method of the request.</param>
    void OnRequestStart(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) { }

    /// <summary>
    /// Called at the end of a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="connectionId">ID of the connection.</param>
    /// <param name="requestId">ID of the request.</param>
    /// <param name="httpVersion">HTTP version of the request.</param>
    /// <param name="path">Path of the request.</param>
    /// <param name="method">HTTP method of the request.</param>
    void OnRequestStop(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) { }
}
