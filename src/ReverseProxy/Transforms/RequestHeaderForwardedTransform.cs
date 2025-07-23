// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// An implementation of the Forwarded header as defined in https://tools.ietf.org/html/rfc7239.
/// </summary>
public class RequestHeaderForwardedTransform : RequestTransform
{
    internal static readonly RequestHeaderForwardedTransform RemoveTransform =
        new RequestHeaderForwardedTransform(new NullRandomFactory(), NodeFormat.Random, NodeFormat.Random, false, false, ForwardedTransformActions.Remove);

    private const string ForwardedHeaderName = "Forwarded";
    // obfnode = "_" 1*( ALPHA / DIGIT / "." / "_" / "-")
    private const string ObfChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

    private readonly IRandomFactory _randomFactory;

    public RequestHeaderForwardedTransform(IRandomFactory randomFactory, NodeFormat forFormat, NodeFormat byFormat, bool host, bool proto, ForwardedTransformActions action)
    {
        ArgumentNullException.ThrowIfNull(randomFactory);
        _randomFactory = randomFactory;
        ForFormat = forFormat;
        ByFormat = byFormat;
        HostEnabled = host;
        ProtoEnabled = proto;
        Debug.Assert(action != ForwardedTransformActions.Off);
        TransformAction = action;
    }

    internal NodeFormat ForFormat { get; }

    internal NodeFormat ByFormat { get; }

    internal bool HostEnabled { get; }

    internal bool ProtoEnabled { get; }

    internal ForwardedTransformActions TransformAction { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.HttpContext;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(context, ForwardedHeaderName);
                AddHeader(context, ForwardedHeaderName, GetHeaderValue(httpContext));
                break;
            case ForwardedTransformActions.Append:
                var existingValues = TakeHeader(context, ForwardedHeaderName);
                var values = StringValues.Concat(existingValues, GetHeaderValue(httpContext));
                AddHeader(context, ForwardedHeaderName, values);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(context, ForwardedHeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

        return default;
    }

    private string GetHeaderValue(HttpContext httpContext)
    {
        var builder = new ValueStringBuilder(stackalloc char[ValueStringBuilder.StackallocThreshold]);
        AppendProto(httpContext, ref builder);
        AppendHost(httpContext, ref builder);
        AppendFor(httpContext, ref builder);
        AppendBy(httpContext, ref builder);
        return builder.ToString();
    }

    private void AppendProto(HttpContext context, ref ValueStringBuilder builder)
    {
        if (ProtoEnabled)
        {
            // Always first doesn't need to check for ';'
            builder.Append("proto=");
            builder.Append(context.Request.Scheme);
        }
    }

    private void AppendHost(HttpContext context, ref ValueStringBuilder builder)
    {
        if (HostEnabled)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }
            // Quoted because of the ':' when there's a port.
            builder.Append("host=\"");
            builder.Append(context.Request.Host.ToUriComponent());
            builder.Append('"');
        }
    }

    private void AppendFor(HttpContext context, ref ValueStringBuilder builder)
    {
        if (ForFormat > NodeFormat.None)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }
            builder.Append("for=");
            AppendNode(context.Connection.RemoteIpAddress, context.Connection.RemotePort, ForFormat, ref builder);
        }
    }

    private void AppendBy(HttpContext context, ref ValueStringBuilder builder)
    {
        if (ByFormat > NodeFormat.None)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }
            builder.Append("by=");
            AppendNode(context.Connection.LocalIpAddress, context.Connection.LocalPort, ByFormat, ref builder);
        }
    }

    // https://tools.ietf.org/html/rfc7239#section-6
    private void AppendNode(IPAddress? ipAddress, int port, NodeFormat format, ref ValueStringBuilder builder)
    {
        // Prefer IPv4 formatting
        if (ipAddress is { IsIPv4MappedToIPv6: true })
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        // "It is important to note that an IPv6 address and any nodename with
        // node-port specified MUST be quoted, since ":" is not an allowed
        // character in "token"."
        var addPort = port != 0 && (format == NodeFormat.IpAndPort || format == NodeFormat.UnknownAndPort || format == NodeFormat.RandomAndPort);
        var addRandomPort = (format == NodeFormat.IpAndRandomPort || format == NodeFormat.UnknownAndRandomPort || format == NodeFormat.RandomAndRandomPort);
        var ipv6 = (format == NodeFormat.Ip || format == NodeFormat.IpAndPort || format == NodeFormat.IpAndRandomPort)
            && ipAddress is not null && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        var quote = addPort || addRandomPort || ipv6;

        if (quote)
        {
            builder.Append('"');
        }

        switch (format)
        {
            case NodeFormat.Ip:
            case NodeFormat.IpAndPort:
            case NodeFormat.IpAndRandomPort:
                if (ipAddress is not null)
                {
                    if (ipv6)
                    {
                        builder.Append('[');
                    }
                    builder.Append(ipAddress.ToString());
                    if (ipv6)
                    {
                        builder.Append(']');
                    }
                    break;
                }
                // This primarily happens in test environments that don't use real connections.
                goto case NodeFormat.Unknown;
            case NodeFormat.Unknown:
            case NodeFormat.UnknownAndPort:
            case NodeFormat.UnknownAndRandomPort:
                builder.Append("unknown");
                break;
            case NodeFormat.Random:
            case NodeFormat.RandomAndPort:
            case NodeFormat.RandomAndRandomPort:
                AppendRandom(ref builder);
                break;
            default:
                throw new NotImplementedException(format.ToString());
        }

        if (addPort)
        {
            builder.Append(':');
            builder.Append(port);
        }
        else if (addRandomPort)
        {
            builder.Append(':');
            AppendRandom(ref builder);
        }

        if (quote)
        {
            builder.Append('"');
        }
    }

    // https://tools.ietf.org/html/rfc7239#section-6.3
    private void AppendRandom(ref ValueStringBuilder builder)
    {
        var random = _randomFactory.CreateRandomInstance();
        builder.Append('_');
        // This length is arbitrary.
        for (var i = 0; i < 9; i++)
        {
            builder.Append(ObfChars[random.Next(ObfChars.Length)]);
        }
    }
}
