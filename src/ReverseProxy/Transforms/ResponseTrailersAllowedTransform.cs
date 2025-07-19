// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Copies only allowed response trailers.
/// </summary>
public class ResponseTrailersAllowedTransform : ResponseTrailersTransform
{
    public ResponseTrailersAllowedTransform(string[] allowedHeaders)
    {
        ArgumentNullException.ThrowIfNull(allowedHeaders);

        AllowedHeaders = allowedHeaders;
        AllowedHeadersSet = new HashSet<string>(allowedHeaders, StringComparer.OrdinalIgnoreCase).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    internal string[] AllowedHeaders { get; }

    private FrozenSet<string> AllowedHeadersSet { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Debug.Assert(context.ProxyResponse is not null);
        Debug.Assert(!context.HeadersCopied);

        // See https://github.com/dotnet/yarp/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/HttpTransformer.cs#L85-L99
        // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
        // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
        var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
        var outgoingTrailers = responseTrailersFeature?.Trailers;
        if (outgoingTrailers is not null && !outgoingTrailers.IsReadOnly)
        {
            // Note that trailers, if any, should already have been declared in Proxy's response
            CopyResponseHeaders(context.ProxyResponse.TrailingHeaders, outgoingTrailers);
        }

        context.HeadersCopied = true;

        return default;
    }

    // See https://github.com/dotnet/yarp/blob/main/src/ReverseProxy/Forwarder/HttpTransformer.cs#:~:text=void-,CopyResponseHeaders
    private void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
    {
        foreach (var header in source.NonValidated)
        {
            var headerName = header.Key;
            if (!AllowedHeadersSet.Contains(headerName))
            {
                continue;
            }

            destination[headerName] = RequestUtilities.Concat(destination[headerName], header.Value);
        }
    }
}
