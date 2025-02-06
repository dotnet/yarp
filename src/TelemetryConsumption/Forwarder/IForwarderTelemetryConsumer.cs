// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of Yarp.ReverseProxy EventSource events.
/// </summary>
public interface IForwarderTelemetryConsumer
{
    /// <summary>
    /// Called before forwarding a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="destinationPrefix"></param>
    void OnForwarderStart(DateTime timestamp, string destinationPrefix) { }

    /// <summary>
    /// Called after forwarding a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="statusCode">The status code returned in the response.</param>
    void OnForwarderStop(DateTime timestamp, int statusCode) { }

    /// <summary>
    /// Called before <see cref="OnForwarderStop(DateTime, int)"/> if forwarding the request failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="error"><see cref="ForwarderError"/> information for the forwarding failure.</param>
    void OnForwarderFailed(DateTime timestamp, ForwarderError error) { }

    /// <summary>
    /// Called when reaching a given stage of forwarding a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="stage">Stage of the forwarding operation.</param>
    void OnForwarderStage(DateTime timestamp, ForwarderStage stage) { }

    /// <summary>
    /// Called periodically while a content transfer is active.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="isRequest">Indicates whether we are transferring the content from the client to the backend or vice-versa.</param>
    /// <param name="contentLength">Number of bytes transferred.</param>
    /// <param name="iops">Number of read/write pairs performed.</param>
    /// <param name="readTime">Time spent reading from the source.</param>
    /// <param name="writeTime">Time spent writing to the destination.</param>
    void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) { }

    /// <summary>
    /// Called after transferring the request or response content.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="isRequest">Indicates whether we transfered the content from the client to the backend or vice-versa.</param>
    /// <param name="contentLength">Number of bytes transferred.</param>
    /// <param name="iops">Number of read/write pairs performed.</param>
    /// <param name="readTime">Time spent reading from the source.</param>
    /// <param name="writeTime">Time spent writing to the destination.</param>
    /// <param name="firstReadTime">Time spent on the first read of the source.</param>
    void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) { }

    /// <summary>
    /// Called before forwarding a request.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="clusterId">Cluster ID</param>
    /// <param name="routeId">Route ID</param>
    /// <param name="destinationId">Destination ID</param>
    void OnForwarderInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId) { }
}
