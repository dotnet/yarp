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
/// Sets or appends the X-Forwarded-Host header with the request's original Host header.
/// </summary>
public class RequestHeaderXForwardedHostTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="action">Action to applied to the header.</param>
    public RequestHeaderXForwardedHostTransform(string headerName, ForwardedTransformActions action)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName);

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
        var host = httpContext.Request.Host;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(proxyRequest, HeaderName);
                if (host.HasValue)
                {
                    AddHeader(proxyRequest, HeaderName, host.ToUriComponent());
                }
                break;
            case ForwardedTransformActions.Append:
                Append(httpContext, proxyRequest, headersCopied, host);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(proxyRequest, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

    }

    private void Append(HttpContext httpContext, HttpRequestMessage proxyRequest, bool headersCopied, HostString host)
    {
        var existingValues = TakeHeader(httpContext, proxyRequest, headersCopied, HeaderName);
        if (!host.HasValue)
        {
            if (!string.IsNullOrEmpty(existingValues))
            {
                AddHeader(proxyRequest, HeaderName, existingValues);
            }
        }
        else
        {
            var values = StringValues.Concat(existingValues, host.ToUriComponent());
            AddHeader(proxyRequest, HeaderName, values);
        }
    }
}
