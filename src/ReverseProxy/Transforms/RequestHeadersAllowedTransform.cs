// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Copies only allowed request headers.
/// </summary>
public class RequestHeadersAllowedTransform : RequestTransform
{
    public RequestHeadersAllowedTransform(string[] allowedHeaders)
    {
        ArgumentNullException.ThrowIfNull(allowedHeaders);

        AllowedHeaders = allowedHeaders;
        AllowedHeadersSet = new HashSet<string>(allowedHeaders, StringComparer.OrdinalIgnoreCase).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    internal string[] AllowedHeaders { get; }

    private FrozenSet<string> AllowedHeadersSet { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Debug.Assert(!context.HeadersCopied);
        Apply(context.HttpContext, context.ProxyRequest);
        context.HeadersCopied = true;
        return default;
    }

    internal override void ApplyFast(HttpContext httpContext, HttpRequestMessage proxyRequest, ref bool headersCopied)
    {
        Debug.Assert(!headersCopied);
        Apply(httpContext, proxyRequest);
        headersCopied = true;
    }

    private void Apply(HttpContext httpContext, HttpRequestMessage proxyRequest)
    {
        foreach (var header in httpContext.Request.Headers)
        {
            var headerName = header.Key;
            var headerValue = header.Value;
            if (!StringValues.IsNullOrEmpty(headerValue)
                && AllowedHeadersSet.Contains(headerName))
            {
                AddHeader(proxyRequest, headerName, headerValue);
            }
        }
    }
}
