// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Forwarder;

public class HttpTransformer
{
    /// <summary>
    /// A default set of transforms that adds X-Forwarded-* headers, removes the original Host value and
    /// copies all other request and response fields and headers, except for some protocol specific values.
    /// </summary>
    public static readonly HttpTransformer Default = TransformBuilder.CreateTransformer(new TransformBuilderContext());

    /// <summary>
    /// An empty transformer that copies all request and response fields and headers, except for some
    /// protocol specific values.
    /// </summary>
    public static readonly HttpTransformer Empty = new HttpTransformer();

    /// <summary>
    /// Used to create derived instances.
    /// </summary>
    protected HttpTransformer() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBodylessStatusCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            // A 1xx response is terminated by the end of the header section; it cannot contain content
            // or trailers.
            // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.2-2
            >= HttpStatusCode.Continue and < HttpStatusCode.OK => true,
            // A 204 response is terminated by the end of the header section; it cannot contain content
            // or trailers.
            // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.5-5
            HttpStatusCode.NoContent => true,
            // Since the 205 status code implies that no additional content will be provided, a server
            // MUST NOT generate content in a 205 response.
            // See https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.6-3
            HttpStatusCode.ResetContent => true,
            _ => false
        };

    /// <summary>
    /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
    /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
    /// See <see cref="RequestUtilities.MakeDestinationAddress(string, PathString, QueryString)"/> for constructing a custom request Uri.
    /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
    /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
    /// This method may be overridden to conditionally produce a response, such as for error conditions, and prevent the request from
    /// being proxied. This is indicated by setting the `HttpResponse.StatusCode` to a value other than 200, or calling `HttpResponse.StartAsync()`,
    /// or writing to the `HttpResponse.Body` or `BodyWriter`.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyRequest">The outgoing proxy request.</param>
    /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create the RequestUri.</param>
    /// <param name="cancellationToken">Indicates that the request is being canceled.</param>
    public virtual ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
#pragma warning disable CS0618 // We're calling the overload without the CancellationToken for backwards compatibility.
        => TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
#pragma warning restore CS0618

    /// <summary>
    /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
    /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
    /// See <see cref="RequestUtilities.MakeDestinationAddress(string, PathString, QueryString)"/> for constructing a custom request Uri.
    /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
    /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
    /// This method may be overridden to conditionally produce a response, such as for error conditions, and prevent the request from
    /// being proxied. This is indicated by setting the `HttpResponse.StatusCode` to a value other than 200, or calling `HttpResponse.StartAsync()`,
    /// or writing to the `HttpResponse.Body` or `BodyWriter`.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyRequest">The outgoing proxy request.</param>
    /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create the RequestUri.</param>
    [Obsolete("This overload of TransformRequestAsync is obsolete. Override and use the overload accepting a CancellationToken instead.")]
    public virtual ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
    {
        foreach (var header in httpContext.Request.Headers)
        {
            var headerName = header.Key;
            var headerValue = header.Value;
            if (RequestUtilities.ShouldSkipRequestHeader(headerName))
            {
                continue;
            }

            RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
        }

        // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
        // If a message is received with both a Transfer-Encoding and a
        // Content-Length header field, the Transfer-Encoding overrides the
        // Content-Length.  Such a message might indicate an attempt to
        // perform request smuggling (Section 9.5) or response splitting
        // (Section 9.4) and ought to be handled as an error.  A sender MUST
        // remove the received Content-Length field prior to forwarding such
        // a message downstream.
        if (httpContext.Request.Headers.ContainsKey(HeaderNames.TransferEncoding)
            && httpContext.Request.Headers.ContainsKey(HeaderNames.ContentLength))
        {
            proxyRequest.Content?.Headers.Remove(HeaderNames.ContentLength);
        }

        // https://datatracker.ietf.org/doc/html/rfc7540#section-8.1.2.2
        // The only exception to this is the TE header field, which MAY be
        // present in an HTTP/2 request; when it is, it MUST NOT contain any
        // value other than "trailers".
        if (ProtocolHelper.IsHttp2OrGreater(httpContext.Request.Protocol))
        {
            var te = httpContext.Request.Headers.GetCommaSeparatedValues(HeaderNames.TE);
            if (te is not null)
            {
                for (var i = 0; i < te.Length; i++)
                {
                    if (string.Equals(te[i], "trailers", StringComparison.OrdinalIgnoreCase))
                    {
                        var added = proxyRequest.Headers.TryAddWithoutValidation(HeaderNames.TE, te[i]);
                        Debug.Assert(added);
                        break;
                    }
                }
            }
        }

        return default;
    }

    /// <summary>
    /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
    /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
    /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
    /// `Transfer-Encoding: chunked`.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination. This can be null if the destination did not respond.</param>
    /// <param name="cancellationToken">Indicates that the request is being canceled.</param>
    /// <returns>A bool indicating if the response should be proxied to the client or not. A derived implementation 
    /// that returns false may send an alternate response inline or return control to the caller for it to retry, respond, 
    /// etc.</returns>
    public virtual ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse, CancellationToken cancellationToken)
#pragma warning disable CS0618 // We're calling the overload without the CancellationToken for backwards compatibility.
        => TransformResponseAsync(httpContext, proxyResponse);
#pragma warning restore CS0618

    /// <summary>
    /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
    /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
    /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
    /// `Transfer-Encoding: chunked`.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination. This can be null if the destination did not respond.</param>
    /// <returns>A bool indicating if the response should be proxied to the client or not. A derived implementation
    /// that returns false may send an alternate response inline or return control to the caller for it to retry, respond,
    /// etc.</returns>
    [Obsolete("This overload of TransformResponseAsync is obsolete. Override and use the overload accepting a CancellationToken instead.")]
    public virtual ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse)
    {
        if (proxyResponse is null)
        {
            return new ValueTask<bool>(false);
        }

        var responseHeaders = httpContext.Response.Headers;
        CopyResponseHeaders(proxyResponse.Headers, responseHeaders);
        if (proxyResponse.Content is not null)
        {
            CopyResponseHeaders(proxyResponse.Content.Headers, responseHeaders);
        }

        // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
        // If a message is received with both a Transfer-Encoding and a
        // Content-Length header field, the Transfer-Encoding overrides the
        // Content-Length.  Such a message might indicate an attempt to
        // perform request smuggling (Section 9.5) or response splitting
        // (Section 9.4) and ought to be handled as an error.  A sender MUST
        // remove the received Content-Length field prior to forwarding such
        // a message downstream.
        if (proxyResponse.Content is not null
            && proxyResponse.Headers.NonValidated.Contains(HeaderNames.TransferEncoding)
            && proxyResponse.Content.Headers.NonValidated.Contains(HeaderNames.ContentLength))
        {
            httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
        }

        // For responses with status codes that shouldn't include a body,
        // we remove the 'Content-Length: 0' header if one is present.
        if (proxyResponse.Content is not null
            && IsBodylessStatusCode(proxyResponse.StatusCode)
            && proxyResponse.Content.Headers.NonValidated.TryGetValues(HeaderNames.ContentLength, out var contentLengthValue)
            && contentLengthValue.ToString() == "0")
        {
            httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
        }

        return new ValueTask<bool>(true);
    }

    /// <summary>
    /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
    /// copied to the HttpContext.Response by the base implementation.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination.</param>
    /// <param name="cancellationToken">Indicates that the request is being canceled.</param>
    public virtual ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
#pragma warning disable CS0618 // We're calling the overload without the CancellationToken for backwards compatibility.
        => TransformResponseTrailersAsync(httpContext, proxyResponse);
#pragma warning restore CS0618

    /// <summary>
    /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
    /// copied to the HttpContext.Response by the base implementation.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination.</param>
    [Obsolete("This overload of TransformResponseTrailersAsync is obsolete. Override and use the overload accepting a CancellationToken instead.")]
    public virtual ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
    {
        // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
        // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
        var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
        var outgoingTrailers = responseTrailersFeature?.Trailers;
        if (outgoingTrailers is not null && !outgoingTrailers.IsReadOnly)
        {
            // Note that trailers, if any, should already have been declared in Proxy's response
            // by virtue of us having proxied all response headers in step 6.
            CopyResponseHeaders(proxyResponse.TrailingHeaders, outgoingTrailers);
        }

        return default;
    }

    private static void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
    {
        // We want to append to any prior values, if any.
        // Not using Append here because it skips empty headers.
        foreach (var header in source.NonValidated)
        {
            var headerName = header.Key;
            if (RequestUtilities.ShouldSkipResponseHeader(headerName))
            {
                continue;
            }

            var currentValue = destination[headerName];

            // https://github.com/dotnet/yarp/issues/2269
            // The Strict-Transport-Security may be added by the proxy before forwarding. Only copy the header
            // if it's not already present.
            if (!StringValues.IsNullOrEmpty(currentValue)
                && string.Equals(headerName, HeaderNames.StrictTransportSecurity, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            destination[headerName] = RequestUtilities.Concat(currentValue, header.Value);
        }
    }
}
