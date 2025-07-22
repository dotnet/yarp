// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Transforms for a given route.
/// </summary>
internal sealed class StructuredTransformer : HttpTransformer
{
    /// <summary>
    /// Creates a new <see cref="StructuredTransformer"/> instance.
    /// </summary>
    internal StructuredTransformer(bool? copyRequestHeaders, bool? copyResponseHeaders, bool? copyResponseTrailers,
        IList<RequestTransform> requestTransforms,
        IList<ResponseTransform> responseTransforms,
        IList<ResponseTrailersTransform> responseTrailerTransforms)
    {
        ArgumentNullException.ThrowIfNull(requestTransforms);
        ArgumentNullException.ThrowIfNull(responseTransforms);
        ArgumentNullException.ThrowIfNull(responseTrailerTransforms);

        ShouldCopyRequestHeaders = copyRequestHeaders;
        ShouldCopyResponseHeaders = copyResponseHeaders;
        ShouldCopyResponseTrailers = copyResponseTrailers;
        RequestTransforms = requestTransforms.ToArray();
        ResponseTransforms = responseTransforms.ToArray();
        ResponseTrailerTransforms = responseTrailerTransforms.ToArray();
    }

    /// <summary>
    /// Indicates if all request headers should be copied to the proxy request before applying transforms.
    /// </summary>
    internal bool? ShouldCopyRequestHeaders { get; }

    /// <summary>
    /// Indicates if all response headers should be copied to the client response before applying transforms.
    /// </summary>
    internal bool? ShouldCopyResponseHeaders { get; }

    /// <summary>
    /// Indicates if all response trailers should be copied to the client response before applying transforms.
    /// </summary>
    internal bool? ShouldCopyResponseTrailers { get; }

    /// <summary>
    /// Request transforms.
    /// </summary>
    internal RequestTransform[] RequestTransforms { get; }

    /// <summary>
    /// Response header transforms.
    /// </summary>
    internal ResponseTransform[] ResponseTransforms { get; }

    /// <summary>
    /// Response trailer transforms.
    /// </summary>
    internal ResponseTrailersTransform[] ResponseTrailerTransforms { get; }

#pragma warning disable CS0672 // We're overriding the obsolete overloads to preserve backwards compatibility.
    public override ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix) =>
        TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, CancellationToken.None);

    public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse) =>
        TransformResponseAsync(httpContext, proxyResponse, CancellationToken.None);

    public override ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse) =>
        TransformResponseTrailersAsync(httpContext, proxyResponse, CancellationToken.None);
#pragma warning restore

    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
    {
        if (ShouldCopyRequestHeaders.GetValueOrDefault(true))
        {
            // We own the base implementation and know it doesn't make use of the cancellation token.
            // We're intentionally calling the overload without it to avoid it calling back into this derived implementation, causing a stack overflow.

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
#pragma warning disable CS0618 // Type or member is obsolete
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
#pragma warning restore CS0618
#pragma warning restore CA2016
        }

        if (RequestTransforms.Length == 0)
        {
            return;
        }

        var transformContext = new RequestTransformContext()
        {
            DestinationPrefix = destinationPrefix,
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
            Path = httpContext.Request.Path,
            HeadersCopied = ShouldCopyRequestHeaders.GetValueOrDefault(true),
            CancellationToken = cancellationToken,
        };

        foreach (var requestTransform in RequestTransforms)
        {
            await requestTransform.ApplyAsync(transformContext);

            // The transform generated a response, do not apply further transforms and do not forward.
            if (RequestUtilities.IsResponseSet(httpContext.Response))
            {
                return;
            }
        }

        // Allow a transform to directly set a custom RequestUri.
        if (proxyRequest.RequestUri is null)
        {
            var queryString = transformContext.MaybeQuery?.QueryString ?? httpContext.Request.QueryString;

            proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, queryString);
        }
    }

    public override async ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse, CancellationToken cancellationToken)
    {
        if (ShouldCopyResponseHeaders.GetValueOrDefault(true))
        {
            // We own the base implementation and know it doesn't make use of the cancellation token.
            // We're intentionally calling the overload without it to avoid it calling back into this derived implementation, causing a stack overflow.

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
#pragma warning disable CS0618 // Type or member is obsolete
            await base.TransformResponseAsync(httpContext, proxyResponse);
#pragma warning restore CS0618
#pragma warning restore CA2016
        }

        if (ResponseTransforms.Length == 0)
        {
            return true;
        }

        var transformContext = new ResponseTransformContext()
        {
            HttpContext = httpContext,
            ProxyResponse = proxyResponse,
            HeadersCopied = ShouldCopyResponseHeaders.GetValueOrDefault(true),
            CancellationToken = cancellationToken,
        };

        foreach (var responseTransform in ResponseTransforms)
        {
            await responseTransform.ApplyAsync(transformContext);
        }

        return !transformContext.SuppressResponseBody;
    }

    public override async ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
    {
        if (ShouldCopyResponseTrailers.GetValueOrDefault(true))
        {
            // We own the base implementation and know it doesn't make use of the cancellation token.
            // We're intentionally calling the overload without it to avoid it calling back into this derived implementation, causing a stack overflow.

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
#pragma warning disable CS0618 // Type or member is obsolete
            await base.TransformResponseTrailersAsync(httpContext, proxyResponse);
#pragma warning restore CS0618
#pragma warning restore CA2016
        }

        if (ResponseTrailerTransforms.Length == 0)
        {
            return;
        }

        // Only run the transforms if trailers are actually supported by the client response.
        var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
        var outgoingTrailers = responseTrailersFeature?.Trailers;
        if (outgoingTrailers is not null && !outgoingTrailers.IsReadOnly)
        {
            var transformContext = new ResponseTrailersTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = ShouldCopyResponseTrailers.GetValueOrDefault(true),
                CancellationToken = cancellationToken,
            };

            foreach (var responseTrailerTransform in ResponseTrailerTransforms)
            {
                await responseTrailerTransform.ApplyAsync(transformContext);
            }
        }
    }
}
