// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Removes a request header.
/// </summary>
public class RequestHeaderRemoveTransform : RequestTransform
{
    public RequestHeaderRemoveTransform(string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
    }

    internal string HeaderName { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RemoveHeader(context.ProxyRequest, HeaderName);
        return default;
    }

    internal override void ApplyFast(HttpContext httpContext, HttpRequestMessage proxyRequest, ref bool headersCopied)
    {
        RemoveHeader(proxyRequest, HeaderName);
    }
}
