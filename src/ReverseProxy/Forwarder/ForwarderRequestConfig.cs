// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Config for <see cref="IHttpForwarder.SendAsync(HttpContext, string, HttpMessageInvoker, ForwarderRequestConfig, HttpTransformer, CancellationToken)"/>
/// </summary>
public sealed record ForwarderRequestConfig
{
    /// <summary>
    /// An empty instance of this type.
    /// </summary>
    public static ForwarderRequestConfig Empty { get; } = new();

    /// <summary>
    /// How long a request is allowed to remain idle between any operation completing, after which it will be canceled.
    /// The default is 100 seconds. The timeout will reset when response headers are received or after successfully reading or
    /// writing any request, response, or streaming data like gRPC or WebSockets. TCP keep-alive packets and HTTP/2 protocol pings will
    /// not reset the timeout, but WebSocket pings will.
    /// </summary>
    public TimeSpan? ActivityTimeout { get; init; }

    /// <summary>
    /// Preferred version of the outgoing request.
    /// The default is HTTP/2.0.
    /// </summary>
    public Version? Version { get; init; }

    /// <summary>
    /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
    /// request an exact version. The default is `RequestVersionOrLower`.
    /// </summary>
    public HttpVersionPolicy? VersionPolicy { get; init; }

    /// <summary>
    /// Allows to use write buffering when sending a response back to the client,
    /// if the server hosting YARP (e.g. IIS) supports it.
    /// NOTE: enabling it can break SSE (server side event) scenarios.
    /// </summary>
    public bool? AllowResponseBuffering { get; init; }

    public bool Equals(ForwarderRequestConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return ActivityTimeout == other.ActivityTimeout
            && VersionPolicy == other.VersionPolicy
            && Version == other.Version
            && AllowResponseBuffering == other.AllowResponseBuffering;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ActivityTimeout,
            VersionPolicy,
            Version,
            AllowResponseBuffering);
    }
}
