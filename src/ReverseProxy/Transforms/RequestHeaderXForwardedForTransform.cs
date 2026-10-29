// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Sets or appends the X-Forwarded-For header with the previous client's IP address.
/// </summary>
public class RequestHeaderXForwardedForTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="action">Action to applied to the header.</param>
    public RequestHeaderXForwardedForTransform(string headerName, ForwardedTransformActions action)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
        Debug.Assert(action != ForwardedTransformActions.Off);
        TransformAction = action;
    }

    internal string HeaderName { get; }

    internal ForwardedTransformActions TransformAction { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Apply(context.HttpContext, context.ProxyRequest, context.HeadersCopied);
        return default;
    }

    internal override void ApplyFast(HttpContext httpContext, HttpRequestMessage proxyRequest, ref bool headersCopied)
    {
        Apply(httpContext, proxyRequest, headersCopied);
    }

    private void Apply(HttpContext httpContext, HttpRequestMessage proxyRequest, bool headersCopied)
    {
        string? remoteIp = null;
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress is not null)
        {
            remoteIp = remoteIpAddress.IsIPv4MappedToIPv6 ?
                remoteIpAddress.MapToIPv4().ToString() :
                remoteIpAddress.ToString();
        }

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(proxyRequest, HeaderName);
                if (remoteIp is not null)
                {
                    AddHeader(proxyRequest, HeaderName, remoteIp);
                }
                break;
            case ForwardedTransformActions.Append:
                Append(httpContext, proxyRequest, headersCopied, remoteIp);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(proxyRequest, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

    }

    private void Append(HttpContext httpContext, HttpRequestMessage proxyRequest, bool headersCopied, string? remoteIp)
    {
        var existingValues = TakeHeader(httpContext, proxyRequest, headersCopied, HeaderName);
        if (remoteIp is null)
        {
            if (!string.IsNullOrEmpty(existingValues))
            {
                AddHeader(proxyRequest, HeaderName, existingValues);
            }
        }
        else
        {
            var values = StringValues.Concat(existingValues, remoteIp);
            AddHeader(proxyRequest, HeaderName, values);
        }
    }
}
