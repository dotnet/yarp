// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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

        foreach (var header in context.HttpContext.Request.Headers)
        {
            var headerName = header.Key;
            var headerValue = header.Value;
            if (!StringValues.IsNullOrEmpty(headerValue)
                && AllowedHeadersSet.Contains(headerName))
            {
                AddHeader(context, headerName, headerValue);
            }
        }

        context.HeadersCopied = true;

        return default;
    }
}
