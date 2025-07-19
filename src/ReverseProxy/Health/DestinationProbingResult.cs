// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Result of a destination's active health probing.
/// </summary>
public readonly struct DestinationProbingResult
{
    public DestinationProbingResult(DestinationState destination, HttpResponseMessage? response, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Destination = destination;
        Response = response;
        Exception = exception;
    }

    /// <summary>
    /// Probed destination.
    /// </summary>
    public DestinationState Destination { get; }

    /// <summary>
    /// Response received.
    /// It can be null in case of a failure.
    /// </summary>
    public HttpResponseMessage? Response { get; }

    /// <summary>
    /// Exception thrown during probing.
    /// It is null in case of a success.
    /// </summary>
    public Exception? Exception { get; }
}
