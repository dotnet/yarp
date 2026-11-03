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
/// Sets or appends the X-Forwarded-Proto header with the request's original url scheme.
/// </summary>
public class RequestHeaderXForwardedProtoTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="action">Action to applied to the header.</param>
    public RequestHeaderXForwardedProtoTransform(string headerName, ForwardedTransformActions action)
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
        var scheme = httpContext.Request.Scheme;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(proxyRequest, HeaderName);
                AddHeader(proxyRequest, HeaderName, scheme);
                break;
            case ForwardedTransformActions.Append:
                var existingValues = TakeHeader(httpContext, proxyRequest, headersCopied, HeaderName);
                var values = StringValues.Concat(existingValues, scheme);
                AddHeader(proxyRequest, HeaderName, values);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(proxyRequest, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

    }
}
