// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of System.Net.Http EventSource events.
/// </summary>
public interface IHttpTelemetryConsumer
{
    /// <summary>
    /// Called before an HTTP request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="scheme">Scheme of the request Uri.</param>
    /// <param name="host">Host of the request Uri.</param>
    /// <param name="port">Port of the request Uri.</param>
    /// <param name="pathAndQuery">Path and query of the request Uri.</param>
    /// <param name="versionMajor">Major component of the request's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the request's HTTP version.</param>
    /// <param name="versionPolicy"><see cref="HttpVersionPolicy"/> of the request.</param>
    void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy) { }

    /// <summary>
    /// Called after an HTTP request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnRequestStop(DateTime timestamp) { }

    /// <summary>
    /// Called before <see cref="OnRequestStop(DateTime)"/> if the request failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnRequestFailed(DateTime timestamp) { }

    /// <summary>
    /// Called when a new HTTP connection is established.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="versionMajor">Major component of the connection's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the connection's HTTP version.</param>
    void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor) { }

    /// <summary>
    /// Called when a new HTTP connection is closed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="versionMajor">Major component of the connection's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the connection's HTTP version.</param>
    void OnConnectionClosed(DateTime timestamp, int versionMajor, int versionMinor) { }

    /// <summary>
    /// Called when a request that hit the MaxConnectionsPerServer or MAX_CONCURRENT_STREAMS limit leaves the queue.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="timeOnQueue">Time spent on queue.</param>
    /// <param name="versionMajor">Major component of the request's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the request's HTTP version.</param>
    void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor) { }

    /// <summary>
    /// Called before sending the request headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnRequestHeadersStart(DateTime timestamp) { }

    /// <summary>
    /// Called after sending the request headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnRequestHeadersStop(DateTime timestamp) { }

    /// <summary>
    /// Called before sending the request content.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnRequestContentStart(DateTime timestamp) { }

    /// <summary>
    /// Called after sending the request content.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="contentLength"></param>
    void OnRequestContentStop(DateTime timestamp, long contentLength) { }

    /// <summary>
    /// Called before reading the response headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResponseHeadersStart(DateTime timestamp) { }

    /// <summary>
    /// Called after reading all response headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResponseHeadersStop(DateTime timestamp) { }

    /// <summary>
    /// Called when <see cref="HttpClient"/> starts buffering the response content.
    /// This event WILL NOT be called for requests made by YARP, as they are not buffered.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResponseContentStart(DateTime timestamp) { }

    /// <summary>
    /// Called when <see cref="HttpClient"/> stops buffering the response content.
    /// This event WILL NOT be called for requests made by YARP, as they are not buffered.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResponseContentStop(DateTime timestamp) { }

    /// <summary>
    /// Called after an HTTP request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="statusCode">The status code returned by the server. -1 if no response was received.</param>
    void OnRequestStop(DateTime timestamp, int statusCode) =>
        OnRequestStop(timestamp);

    /// <summary>
    /// Called before <see cref="OnRequestStop(DateTime)"/> if the request failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="exceptionMessage">A message that describes the exception associated with this request failure.</param>
    void OnRequestFailed(DateTime timestamp, string exceptionMessage) =>
        OnRequestFailed(timestamp);

    /// <summary>
    /// Called when a new HTTP connection is established.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="versionMajor">Major component of the connection's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the connection's HTTP version.</param>
    /// <param name="connectionId">ID of the connection that was established, unique for this process.</param>
    /// <param name="scheme">Scheme the connection was established with.</param>
    /// <param name="host">Host the connection was established to.</param>
    /// <param name="port">Port the connection was established to.</param>
    /// <param name="remoteAddress">The remote address this connection was established to, if available.</param>
    void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor, long connectionId, string scheme, string host, int port, string? remoteAddress) =>
        OnConnectionEstablished(timestamp, versionMajor, versionMinor);

    /// <summary>
    /// Called when a new HTTP connection is closed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="versionMajor">Major component of the connection's HTTP version.</param>
    /// <param name="versionMinor">Minor component of the connection's HTTP version.</param>
    /// <param name="connectionId">ID of the connection that was closed.</param>
    void OnConnectionClosed(DateTime timestamp, int versionMajor, int versionMinor, long connectionId) =>
        OnConnectionClosed(timestamp, versionMajor, versionMinor);

    /// <summary>
    /// Called before sending the request headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="connectionId">ID of the connection we are sending this request on.</param>
    void OnRequestHeadersStart(DateTime timestamp, long connectionId) =>
        OnRequestHeadersStart(timestamp);

    /// <summary>
    /// Called after reading all response headers.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="statusCode">The status code returned by the server.</param>
    void OnResponseHeadersStop(DateTime timestamp, int statusCode) =>
        OnResponseHeadersStop(timestamp);

    /// <summary>
    /// Called before a request is redirected if <see cref="SocketsHttpHandler.AllowAutoRedirect"/> is enabled.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="redirectUri">The uri the request is being redirected to.</param>
    void OnRedirect(DateTime timestamp, string redirectUri) { }
}
