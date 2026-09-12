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
/// Sets or appends the X-Forwarded-Prefix header with the request's original PathBase.
/// </summary>
public class RequestHeaderXForwardedPrefixTransform : RequestTransform
{
    public RequestHeaderXForwardedPrefixTransform(string headerName, ForwardedTransformActions action)
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
        var pathBase = httpContext.Request.PathBase;

        switch (TransformAction)
        {
            case ForwardedTransformActions.Set:
                RemoveHeader(proxyRequest, HeaderName);
                if (pathBase.HasValue)
                {
                    AddHeader(proxyRequest, HeaderName, pathBase.ToUriComponent());
                }
                break;
            case ForwardedTransformActions.Append:
                Append(httpContext, proxyRequest, headersCopied, pathBase);
                break;
            case ForwardedTransformActions.Remove:
                RemoveHeader(proxyRequest, HeaderName);
                break;
            default:
                throw new NotImplementedException(TransformAction.ToString());
        }

    }

    private void Append(HttpContext httpContext, HttpRequestMessage proxyRequest, bool headersCopied, PathString pathBase)
    {
        var existingValues = TakeHeader(httpContext, proxyRequest, headersCopied, HeaderName);
        if (!pathBase.HasValue)
        {
            if (!string.IsNullOrEmpty(existingValues))
            {
                AddHeader(proxyRequest, HeaderName, existingValues);
            }
        }
        else
        {
            var values = StringValues.Concat(existingValues, pathBase.ToUriComponent());
            AddHeader(proxyRequest, HeaderName, values);
        }
    }
}
