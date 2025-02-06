// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Options used for communicating with the destination servers.
/// </summary>
/// <remarks>
/// If you need a more granular approach, please use a <see href="https://microsoft.github.io/reverse-proxy/articles/http-client-config.html#custom-iforwarderhttpclientfactory">custom implementation of <see cref="IForwarderHttpClientFactory"/></see>.
/// </remarks>
public sealed record HttpClientConfig
{
    /// <summary>
    /// An empty options instance.
    /// </summary>
    public static readonly HttpClientConfig Empty = new();

    /// <summary>
    /// What TLS protocols to use.
    /// </summary>
    public SslProtocols? SslProtocols { get; init; }

    /// <summary>
    /// Indicates if destination server https certificate errors should be ignored.
    /// This should only be done when using self-signed certificates.
    /// </summary>
    public bool? DangerousAcceptAnyServerCertificate { get; init; }

    /// <summary>
    /// Limits the number of connections used when communicating with the destination server.
    /// </summary>
    public int? MaxConnectionsPerServer { get; init; }

    /// <summary>
    /// Optional web proxy used when communicating with the destination server.
    /// </summary>
    public WebProxyConfig? WebProxy { get; init; }

    /// <summary>
    /// Gets or sets a value that indicates whether additional HTTP/2 connections can
    /// be established to the same server when the maximum number of concurrent streams
    /// is reached on all existing connections.
    /// </summary>
    public bool? EnableMultipleHttp2Connections { get; init; }

    /// <summary>
    /// Allows overriding the default (ASCII) encoding for outgoing request headers.
    /// <para>
    /// Setting this value will in turn set <see cref="SocketsHttpHandler.RequestHeaderEncodingSelector"/> and use the selected encoding for all request headers.
    /// The value is then parsed by <see cref="Encoding.GetEncoding(string)"/>, so use values like: "utf-8", "iso-8859-1", etc.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Note: If you're using an encoding other than UTF-8 here, then you may also need to configure your server to accept request headers with such an encoding via the corresponding options for the server.
    /// <para>
    /// For example, when using Kestrel as the server, use <see cref="KestrelServerOptions.RequestHeaderEncodingSelector"/> to
    /// <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/options">configure Kestrel</see> to use the same encoding.
    /// </para>
    /// </remarks>
    public string? RequestHeaderEncoding { get; init; }

    /// <summary>
    /// Allows overriding the default (Latin1) encoding for incoming request headers.
    /// <para>
    /// Setting this value will in turn set <see cref="SocketsHttpHandler.ResponseHeaderEncodingSelector"/> and use the selected encoding for all response headers.
    /// The value is then parsed by <see cref="Encoding.GetEncoding(string)"/>, so use values like: "utf-8", "iso-8859-1", etc.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Note: If you're using an encoding other than ASCII here, then you may also need to configure your server to send response headers with such an encoding via the corresponding options for the server.
    /// <para>
    /// For example, when using Kestrel as the server, use <see cref="KestrelServerOptions.ResponseHeaderEncodingSelector"/> to
    /// <see href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/options">configure Kestrel</see> to use the same encoding.
    /// </para>
    /// </remarks>
    public string? ResponseHeaderEncoding { get; init; }

    public bool Equals(HttpClientConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return SslProtocols == other.SslProtocols
               && DangerousAcceptAnyServerCertificate == other.DangerousAcceptAnyServerCertificate
               && MaxConnectionsPerServer == other.MaxConnectionsPerServer
               && EnableMultipleHttp2Connections == other.EnableMultipleHttp2Connections
               // Comparing by reference is fine here since Encoding.GetEncoding returns the same instance for each encoding.
               && RequestHeaderEncoding == other.RequestHeaderEncoding
               && ResponseHeaderEncoding == other.ResponseHeaderEncoding
               && WebProxy == other.WebProxy;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SslProtocols,
            DangerousAcceptAnyServerCertificate,
            MaxConnectionsPerServer,
            EnableMultipleHttp2Connections,
            RequestHeaderEncoding,
            ResponseHeaderEncoding,
            WebProxy);
    }
}
