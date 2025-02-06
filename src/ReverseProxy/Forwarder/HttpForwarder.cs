// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Timeouts;
#endif
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Default implementation of <see cref="IHttpForwarder"/>.
/// </summary>
internal sealed class HttpForwarder : IHttpForwarder
{
    private const string WebSocketName = "websocket";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);
    private static readonly Version DefaultVersion = HttpVersion.Version20;
    private const HttpVersionPolicy DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    public HttpForwarder(ILogger<HttpForwarder> logger, TimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Proxies the incoming request to the destination server, and the response back to the client.
    /// </summary>
    /// <remarks>
    /// In what follows, as well as throughout in Reverse Proxy, we consider
    /// the following picture as illustrative of the Proxy.
    /// <code>
    ///      +-------------------+
    ///      |  Destination      +
    ///      +-------------------+
    ///            ▲       |
    ///        (b) |       | (c)
    ///            |       ▼
    ///      +-------------------+
    ///      |      Proxy        +
    ///      +-------------------+
    ///            ▲       |
    ///        (a) |       | (d)
    ///            |       ▼
    ///      +-------------------+
    ///      | Client            +
    ///      +-------------------+
    /// </code>
    ///
    /// (a) and (b) show the *request* path, going from the client to the target.
    /// (c) and (d) show the *response* path, going from the destination back to the client.
    ///
    /// Normal proxying comprises the following steps:
    ///    (0) Disable ASP .NET Core limits for streaming requests
    ///    (1) Create outgoing HttpRequestMessage
    ///    (2) Setup copy of request body (background)             Client --► Proxy --► Destination
    ///    (3) Copy request headers                                Client --► Proxy --► Destination
    ///    (4) Send the outgoing request using HttpMessageInvoker  Client --► Proxy --► Destination
    ///    (5) Copy response status line                           Client ◄-- Proxy ◄-- Destination
    ///    (6) Copy response headers                               Client ◄-- Proxy ◄-- Destination
    ///    (7-A) Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol.
    ///        (7-A-1)  Upgrade client channel                     Client ◄--- Proxy ◄--- Destination
    ///        (7-A-2)  Copy duplex streams and return             Client ◄--► Proxy ◄--► Destination
    ///    (7-B) Copy (normal) response body                       Client ◄-- Proxy ◄-- Destination
    ///    (8) Copy response trailer headers and finish response   Client ◄-- Proxy ◄-- Destination
    ///    (9) Wait for completion of step 2: copying request body Client --► Proxy --► Destination
    ///
    /// ASP .NET Core (Kestrel) will finally send response trailers (if any)
    /// after we complete the steps above and relinquish control.
    /// </remarks>
    public ValueTask<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig,
        HttpTransformer transformer)
        => SendAsync(context, destinationPrefix, httpClient, requestConfig, transformer, CancellationToken.None);

    public async ValueTask<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig requestConfig,
        HttpTransformer transformer,
        CancellationToken cancellationToken)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        _ = destinationPrefix ?? throw new ArgumentNullException(nameof(destinationPrefix));
        _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ = requestConfig ?? throw new ArgumentNullException(nameof(requestConfig));
        _ = transformer ?? throw new ArgumentNullException(nameof(transformer));

        if (RequestUtilities.IsResponseSet(context.Response))
        {
            throw new InvalidOperationException("The request cannot be forwarded, the response has already started");
        }

        // HttpClient overload for SendAsync changes response behavior to fully buffered which impacts performance
        // See discussion in https://github.com/microsoft/reverse-proxy/issues/458
        if (httpClient is HttpClient)
        {
            throw new ArgumentException($"The http client must be of type HttpMessageInvoker, not HttpClient", nameof(httpClient));
        }

        // "http://a".Length = 8
        if (destinationPrefix is null || destinationPrefix.Length < 8)
        {
            throw new ArgumentException("Invalid destination prefix.", nameof(destinationPrefix));
        }

        ForwarderTelemetry.Log.ForwarderStart(destinationPrefix);

        var activityCancellationSource = ActivityCancellationTokenSource.Rent(requestConfig?.ActivityTimeout ?? DefaultTimeout, context.RequestAborted, cancellationToken);
        try
        {
            var isClientHttp2OrGreater = ProtocolHelper.IsHttp2OrGreater(context.Request.Protocol);

            // NOTE: We heuristically assume gRPC-looking requests may require streaming semantics.
            // See https://github.com/microsoft/reverse-proxy/issues/118 for design discussion.
            var isStreamingRequest = isClientHttp2OrGreater && ProtocolHelper.IsGrpcContentType(context.Request.ContentType);

            HttpRequestMessage? destinationRequest = null;
            StreamCopyHttpContent? requestContent = null;
            HttpResponseMessage destinationResponse;
            try
            {
                // :: Step 1-3: Create outgoing HttpRequestMessage
                bool tryDowngradingH2WsOnFailure;
                (destinationRequest, requestContent, tryDowngradingH2WsOnFailure) = await CreateRequestMessageAsync(
                    context, destinationPrefix, transformer, requestConfig, isStreamingRequest, activityCancellationSource);

                // Transforms generated a response, do not proxy.
                if (RequestUtilities.IsResponseSet(context.Response))
                {
                    Log.NotProxying(_logger, context.Response.StatusCode);
                    return ForwarderError.None;
                }

                Log.Proxying(_logger, destinationRequest, isStreamingRequest);

                // :: Step 4: Send the outgoing request using HttpClient
                ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.SendAsyncStart);

                try
                {
                    destinationResponse = await httpClient.SendAsync(destinationRequest, activityCancellationSource.Token);
                }
                catch (HttpRequestException hre) when (tryDowngradingH2WsOnFailure)
                {
                    Debug.Assert(requestContent is null);
                    // This is how SocketsHttpHandler communicates to us that we tried a HTTP/2 extension that wasn't
                    // enabled by the server. We should retry on HTTP/1.1.
                    if (hre.Data.Contains("SETTINGS_ENABLE_CONNECT_PROTOCOL"))
                    {
                        Debug.Assert(false == (bool?)hre.Data["SETTINGS_ENABLE_CONNECT_PROTOCOL"]);
                        Log.RetryingWebSocketDowngradeNoConnect(_logger);
                    }
                    // This is how SocketsHttpHandler communicates to us that we tried HTTP/2, but the server only supports
                    // HTTP/1.x (as determined by ALPN). We'll only get this when using TLS/https. Retry on HTTP/1.1.
                    // We don't let SocketsHttpHandler downgrade automatically for us because we need to send different headers.
                    else if (hre.Data.Contains("HTTP2_ENABLED"))
                    {
                        Debug.Assert(false == (bool?)hre.Data["HTTP2_ENABLED"]);
                        Log.RetryingWebSocketDowngradeNoHttp2(_logger);
                    }
                    else
                    {
                        throw;
                    }

                    // Trying again
                    activityCancellationSource.ResetTimeout();

                    var config = requestConfig! with
                    {
                        Version = HttpVersion.Version11,
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };

                    // Set the request back to null while we call into CreateRequestMessageAsync so that
                    // potential exceptions are correctly treated as 'RequestCreation'.
                    destinationRequest = null;

                    (destinationRequest, requestContent, _) = await CreateRequestMessageAsync(
                        context, destinationPrefix, transformer, config, isStreamingRequest, activityCancellationSource);

                    destinationResponse = await httpClient.SendAsync(destinationRequest, activityCancellationSource.Token);
                }
            }
            catch (Exception requestException)
            {
                return await HandleRequestFailureAsync(context, requestContent, requestException, transformer, activityCancellationSource,
                    failedDuringRequestCreation: destinationRequest is null);
            }

            ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.SendAsyncStop);
            // Reset the timeout since we received the response headers.
            activityCancellationSource.ResetTimeout();
            Log.ResponseReceived(_logger, destinationResponse);

            try
            {
                // :: Step 5: Copy response status line Client ◄-- Proxy ◄-- Destination
                // :: Step 6: Copy response headers Client ◄-- Proxy ◄-- Destination
                var copyBody = await CopyResponseStatusAndHeadersAsync(destinationResponse, context, transformer, activityCancellationSource.Token);

                if (!copyBody)
                {
                    // The transforms callback decided that the response body should be discarded.
                    destinationResponse.Dispose();

                    if (requestContent is not null && requestContent.InProgress)
                    {
                        activityCancellationSource.Cancel();
                        await requestContent.ConsumptionTask;
                    }

                    return ForwarderError.None;
                }
            }
            catch (Exception ex)
            {
                destinationResponse.Dispose();

                if (requestContent is not null && requestContent.InProgress)
                {
                    activityCancellationSource.Cancel();
                    await requestContent.ConsumptionTask;
                }

                ReportProxyError(context, ForwarderError.ResponseHeaders, ex);
                // Clear the response since status code, reason and some headers might have already been copied and we want clean 502 response.
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return ForwarderError.ResponseHeaders;
            }

            // :: Step 7-A: Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol.
            // Also check for HTTP/2 CONNECT 200 responses, they function similarly.
            if (destinationResponse.StatusCode == HttpStatusCode.SwitchingProtocols
#if NET7_0_OR_GREATER
                || (destinationResponse.StatusCode == HttpStatusCode.OK
                && destinationResponse.Version == HttpVersion.Version20
                && destinationRequest.Headers.Protocol is not null
                && destinationRequest.Method.Equals(HttpMethod.Connect))
#endif
                )
            {
                Debug.Assert(requestContent?.Started != true);
                return await HandleUpgradedResponse(context, destinationResponse, activityCancellationSource);
            }

            // NOTE: it may *seem* wise to call `context.Response.StartAsync()` at this point
            // since it looks like we are ready to send back response headers
            // (and this might help reduce extra delays while we wait to receive the body from the destination).
            // HOWEVER, this would produce the wrong result if it turns out that there is no content
            // from the destination -- instead of sending headers and terminating the stream at once,
            // we would send headers thinking a body may be coming, and there is none.
            // This is problematic on gRPC connections when the destination server encounters an error,
            // in which case it immediately returns the response headers and trailing headers, but no content,
            // and clients misbehave if the initial headers response does not indicate stream end.

            // :: Step 7-B: Copy response body Client ◄-- Proxy ◄-- Destination
            StreamCopyResult responseBodyCopyResult;
            Exception? responseBodyException;

            using (var destinationResponseStream = await destinationResponse.Content.ReadAsStreamAsync(activityCancellationSource.Token))
            {
                // The response content-length is enforced by the server.
                (responseBodyCopyResult, responseBodyException) = await StreamCopier.CopyAsync(isRequest: false, destinationResponseStream, context.Response.Body, StreamCopier.UnknownLength, _timeProvider, activityCancellationSource, activityCancellationSource.Token);
            }

            if (responseBodyCopyResult != StreamCopyResult.Success)
            {
                return await HandleResponseBodyErrorAsync(context, requestContent, responseBodyCopyResult, responseBodyException!, activityCancellationSource);
            }

            // :: Step 8: Copy response trailer headers and finish response Client ◄-- Proxy ◄-- Destination
            await CopyResponseTrailingHeadersAsync(destinationResponse, context, transformer, activityCancellationSource.Token);

            if (isStreamingRequest)
            {
                // NOTE: We must call `CompleteAsync` so that Kestrel will flush all bytes to the client.
                // In the case where there was no response body,
                // this is also when headers and trailing headers are sent to the client.
                // Without this, the client might wait forever waiting for response bytes,
                // while we might wait forever waiting for request bytes,
                // leading to a stuck connection and no way to make progress.
                await context.Response.CompleteAsync();
            }

            // :: Step 9: Wait for completion of step 2: copying request body Client --► Proxy --► Destination
            // NOTE: It is possible for the request body to NOT be copied even when there was an incoming request body,
            // e.g. when the request includes header `Expect: 100-continue` and the destination produced a non-1xx response.
            // We must only wait for the request body to complete if it actually started,
            // otherwise we run the risk of waiting indefinitely for a task that will never complete.
            if (requestContent is not null && requestContent.Started)
            {
                var (requestBodyCopyResult, requestBodyException) = await requestContent.ConsumptionTask;

                if (requestBodyCopyResult != StreamCopyResult.Success)
                {
                    // The response succeeded. If there was a request body error then it was probably because the client or destination decided
                    // to cancel it. Report as low severity.

                    var error = requestBodyCopyResult switch
                    {
                        StreamCopyResult.InputError => ForwarderError.RequestBodyClient,
                        StreamCopyResult.OutputError => ForwarderError.RequestBodyDestination,
                        StreamCopyResult.Canceled => ForwarderError.RequestBodyCanceled,
                        _ => throw new NotImplementedException(requestBodyCopyResult.ToString())
                    };
                    ReportProxyError(context, error, requestBodyException!);
                    return error;
                }
            }
        }
        finally
        {
            activityCancellationSource.Return();
            ForwarderTelemetry.Log.ForwarderStop(context.Response.StatusCode);
        }

        return ForwarderError.None;
    }

    private async ValueTask<(HttpRequestMessage, StreamCopyHttpContent?, bool)> CreateRequestMessageAsync(HttpContext context, string destinationPrefix,
        HttpTransformer transformer, ForwarderRequestConfig? requestConfig, bool isStreamingRequest, ActivityCancellationTokenSource activityToken)
    {
        var destinationRequest = new HttpRequestMessage();

        var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
        var upgradeHeader = context.Request.Headers[HeaderNames.Upgrade].ToString();

        var isSpdyRequest = (upgradeFeature?.IsUpgradableRequest ?? false)
            && upgradeHeader.StartsWith("SPDY/", StringComparison.OrdinalIgnoreCase);
        var isH1WsRequest = (upgradeFeature?.IsUpgradableRequest ?? false)
            && string.Equals(WebSocketName, upgradeHeader, StringComparison.OrdinalIgnoreCase);
        var incomingUpgrade = isSpdyRequest || isH1WsRequest;
        var isH2WsRequest = false;
#if NET7_0_OR_GREATER
        var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
        var connectProtocol = connectFeature?.Protocol;
        isH2WsRequest = (connectFeature?.IsExtendedConnect ?? false)
            && string.Equals(WebSocketName, connectProtocol, StringComparison.OrdinalIgnoreCase);
#endif

        var outgoingHttps = destinationPrefix.StartsWith("https://");
        var outgoingVersion = requestConfig?.Version ?? DefaultVersion;
        var outgoingPolicy = requestConfig?.VersionPolicy ?? DefaultVersionPolicy;
        var outgoingUpgrade = false;
        var outgoingConnect = false;
        var tryDowngradingH2WsOnFailure = false;
        if (isSpdyRequest)
        {
            // Can only be done on HTTP/1.1, force regardless of options.
            outgoingUpgrade = true;
        }
        else if (isH1WsRequest || isH2WsRequest)
        {
            switch (outgoingVersion.Major, outgoingPolicy, outgoingHttps)
            {
#if NET7_0_OR_GREATER
                case (2, HttpVersionPolicy.RequestVersionExact, _):
                case (2, HttpVersionPolicy.RequestVersionOrHigher, true):
                    outgoingConnect = true;
                    break;
                case (1, HttpVersionPolicy.RequestVersionOrHigher, true):
                case (2, HttpVersionPolicy.RequestVersionOrLower, true):
                case (3, HttpVersionPolicy.RequestVersionOrLower, true):
                    // Try H2WS, downgrade if needed.
                    outgoingConnect = true;
                    tryDowngradingH2WsOnFailure = true;
                    break;
#endif
                // 1.x Lower or Exact, regardless of HTTPS
                // Anything else without HTTPS except 2 Exact
                default:
                    // Override to use HTTP/1.1, nothing else is supported.
                    outgoingUpgrade = true;
                    break;
            }
        }

        if (outgoingUpgrade)
        {
            // Can only be done on HTTP/1.1, force regardless of options.
            destinationRequest.Version = HttpVersion.Version11;
            destinationRequest.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            destinationRequest.Method = HttpMethod.Get;
        }
#if NET7_0_OR_GREATER
        else if (outgoingConnect)
        {
            // HTTP/2 only (for now).
            destinationRequest.Version = HttpVersion.Version20;
            destinationRequest.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            destinationRequest.Method = HttpMethod.Connect;
            destinationRequest.Headers.Protocol = connectProtocol ?? WebSocketName;
        }
#endif
        else
        {
            destinationRequest.Method = RequestUtilities.GetHttpMethod(context.Request.Method);
            destinationRequest.Version = outgoingVersion;
            destinationRequest.VersionPolicy = outgoingPolicy;
        }

        // :: Step 2: Setup copy of request body (background) Client --► Proxy --► Destination
        // Note that we must do this before step (3) because step (3) may also add headers to the HttpContent that we set up here.
        var requestContent = SetupRequestBodyCopy(context, isStreamingRequest, activityToken);
        destinationRequest.Content = requestContent;

        // :: Step 3: Copy request headers Client --► Proxy --► Destination
        await transformer.TransformRequestAsync(context, destinationRequest, destinationPrefix, activityToken.Token);

        if (!ReferenceEquals(requestContent, destinationRequest.Content) && destinationRequest.Content is not EmptyHttpContent)
        {
            throw new InvalidOperationException("Replacing the YARP outgoing request HttpContent is not supported. You should configure the HttpContext.Request instead.");
        }

        // The transformer generated a response, do not forward.
        if (RequestUtilities.IsResponseSet(context.Response))
        {
            return (destinationRequest, requestContent, false);
        }

        // Transforms may have taken a while, especially if they buffered the body, they count as forward progress.
        activityToken.ResetTimeout();

        FixupUpgradeRequestHeaders(context, destinationRequest, outgoingUpgrade, outgoingConnect);

        // Allow someone to custom build the request uri, otherwise provide a default for them.
        var request = context.Request;
        destinationRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(destinationPrefix, request.Path, request.QueryString);

        if (requestConfig?.AllowResponseBuffering != true)
        {
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        }

        return (destinationRequest, requestContent, tryDowngradingH2WsOnFailure);
    }

    // Connection and Upgrade headers were not copied with the rest of the headers.
    private void FixupUpgradeRequestHeaders(HttpContext context, HttpRequestMessage request, bool outgoingUpgrade, bool outgoingConnect)
    {
        if (outgoingUpgrade)
        {
            // H2->H1, add Connection, Upgrade, Sec-WebSocket-Key
            if (HttpProtocol.IsHttp2(context.Request.Protocol))
            {
                request.Headers.TryAddWithoutValidation(HeaderNames.Connection, HeaderNames.Upgrade);
                request.Headers.TryAddWithoutValidation(HeaderNames.Upgrade, WebSocketName);
                var key = ProtocolHelper.CreateSecWebSocketKey();
                request.Headers.TryAddWithoutValidation(HeaderNames.SecWebSocketKey, key);
            }
            // H1->H1, re-add the original Connection, Upgrade headers.
            else
            {
                var connectionValues = context.Request.Headers.GetCommaSeparatedValues(HeaderNames.Connection);
                string? connectionUpgradeValue = null;
                foreach (var headerValue in connectionValues)
                {
                    if (headerValue.Equals(HeaderNames.Upgrade, StringComparison.OrdinalIgnoreCase))
                    {
                        // Preserve original value, case
                        connectionUpgradeValue = headerValue;
                        break;
                    }
                }

                if (connectionUpgradeValue is not null && context.Request.Headers.TryGetValue(HeaderNames.Upgrade, out var upgradeValue))
                {
                    request.Headers.TryAddWithoutValidation(HeaderNames.Connection, connectionUpgradeValue);
                    request.Headers.TryAddWithoutValidation(HeaderNames.Upgrade, (IEnumerable<string>)upgradeValue);
                }
            }
        }
        // H1->H2, remove Sec-WebSocket-Key
        else if (outgoingConnect && !HttpProtocol.IsHttp2(context.Request.Protocol))
        {
            var key = context.Request.Headers[HeaderNames.SecWebSocketKey];
            if (!ProtocolHelper.CheckSecWebSocketKey(key))
            {
                Log.InvalidSecWebSocketKeyHeader(_logger, key);
                // The request will not be forwarded if we change the status code.
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            request.Headers.Remove(HeaderNames.SecWebSocketKey);
        }
        // else not an upgrade, or H2->H2, no changes needed
    }

    private StreamCopyHttpContent? SetupRequestBodyCopy(HttpContext context, bool isStreamingRequest, ActivityCancellationTokenSource activityToken)
    {
        // If we generate an HttpContent without a Content-Length then for HTTP/1.1 HttpClient will add a Transfer-Encoding: chunked header
        // even if it's a GET request. Some servers reject requests containing a Transfer-Encoding header if they're not expecting a body.
        // Try to be as specific as possible about the client's intent to send a body. The one thing we don't want to do is to start
        // reading the body early because that has side effects like 100-continue.
        var request = context.Request;
        var hasBody = true;
        var contentLength = request.Headers.ContentLength;
        var method = request.Method;

        var canHaveBodyFeature = request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>();
        if (canHaveBodyFeature is not null)
        {
            // 5.0 servers provide a definitive answer for us.
            hasBody = canHaveBodyFeature.CanHaveBody;

#if NET7_0
            // TODO: Kestrel 7.0 bug only, hasBody shouldn't be true for ExtendedConnect.
            // https://github.com/dotnet/aspnetcore/issues/46002 Fixed in 8.0
            var connectFeature = request.HttpContext.Features.Get<IHttpExtendedConnectFeature>();
            if (connectFeature?.IsExtendedConnect == true)
            {
                hasBody = false;
            }
#endif
        }
        // https://tools.ietf.org/html/rfc7230#section-3.3.3
        // All HTTP/1.1 requests should have Transfer-Encoding or Content-Length.
        // Http.Sys/IIS will even add a Transfer-Encoding header to HTTP/2 requests with bodies for back-compat.
        // HTTP/1.0 Connection: close bodies are only allowed on responses, not requests.
        // https://tools.ietf.org/html/rfc1945#section-7.2.2
        //
        // Transfer-Encoding overrides Content-Length per spec
        else if (request.Headers.TryGetValue(HeaderNames.TransferEncoding, out var transferEncoding)
            && transferEncoding.Count == 1
            && string.Equals("chunked", transferEncoding.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            hasBody = true;
        }
        else if (contentLength.HasValue)
        {
            hasBody = contentLength > 0;
        }
        // Kestrel HTTP/2: There are no required headers that indicate if there is a request body, so we need to sniff other fields.
        else if (!ProtocolHelper.IsHttp2OrGreater(request.Protocol))
        {
            hasBody = false;
        }
        // https://tools.ietf.org/html/rfc7231#section-4.3.1
        // A payload within a GET/HEAD/DELETE/CONNECT request message has no defined semantics; sending a payload body on a
        // GET/HEAD/DELETE/CONNECT request might cause some existing implementations to reject the request.
        // https://tools.ietf.org/html/rfc7231#section-4.3.8
        // A client MUST NOT send a message body in a TRACE request.
        else if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsDelete(method)
            || HttpMethods.IsConnect(method)
            || HttpMethods.IsTrace(method))
        {
            hasBody = false;
        }
        // else hasBody defaults to true

        if (hasBody)
        {
            return new StreamCopyHttpContent(context, isStreamingRequest, _timeProvider, _logger, activityToken);
        }

        return null;
    }

    private ForwarderError HandleRequestBodyFailure(HttpContext context, StreamCopyResult requestBodyCopyResult, Exception requestBodyException, Exception additionalException, bool timedOut)
    {
        ForwarderError requestBodyError;
        int statusCode;
        switch (requestBodyCopyResult)
        {
            // Failed while trying to copy the request body from the client. It's ambiguous if the request or response failed first.
            case StreamCopyResult.InputError:
                requestBodyError = ForwarderError.RequestBodyClient;
                statusCode = timedOut
                    ? StatusCodes.Status408RequestTimeout
                    // Attempt to capture the status code from the request exception if available.
                    : requestBodyException is BadHttpRequestException badHttpRequestException
                        ? badHttpRequestException.StatusCode
                        : StatusCodes.Status400BadRequest;
                break;
            // Failed while trying to copy the request body to the destination. It's ambiguous if the request or response failed first.
            case StreamCopyResult.OutputError:
                requestBodyError = ForwarderError.RequestBodyDestination;
                statusCode = timedOut ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status502BadGateway;
                break;
            default:
                throw new NotImplementedException(requestBodyCopyResult.ToString());
        }

        ReportProxyError(context, requestBodyError, new AggregateException(requestBodyException, additionalException));

        // We don't know if the client is still around to see this error, but set it for diagnostics to see.
        if (!context.Response.HasStarted)
        {
            // Nothing has been sent to the client yet, we can still send a good error response.
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            return requestBodyError;
        }

        ResetOrAbort(context, isCancelled: requestBodyCopyResult == StreamCopyResult.Canceled);

        return requestBodyError;
    }

    private async ValueTask<ForwarderError> HandleRequestFailureAsync(HttpContext context, StreamCopyHttpContent? requestContent, Exception requestException,
        HttpTransformer transformer, ActivityCancellationTokenSource requestCancellationSource, bool failedDuringRequestCreation)
    {
        var triedRequestBody = requestContent?.ConsumptionTask.IsCompleted == true;

        if (requestCancellationSource.CancelledByLinkedToken)
        {
            var requestBodyCanceled = false;
            if (triedRequestBody)
            {
                var (requestBodyCopyResult, requestBodyException) = requestContent!.ConsumptionTask.Result;
                requestBodyCanceled = requestBodyCopyResult == StreamCopyResult.Canceled;
                if (requestBodyCanceled)
                {
                    requestException = new AggregateException(requestException, requestBodyException!);
                }
            }
            // Either the client went away (HttpContext.RequestAborted) or the CancellationToken provided to SendAsync was signaled.
            return await ReportErrorAsync(requestBodyCanceled ? ForwarderError.RequestBodyCanceled : ForwarderError.RequestCanceled,
                context.RequestAborted.IsCancellationRequested ? StatusCodes.Status400BadRequest : StatusCodes.Status502BadGateway);
        }

        // Check for request body errors, these may have triggered the response error.
        if (triedRequestBody)
        {
            var (requestBodyCopyResult, requestBodyException) = requestContent!.ConsumptionTask.Result;

            if (requestBodyCopyResult is StreamCopyResult.InputError or StreamCopyResult.OutputError)
            {
                var error = HandleRequestBodyFailure(context, requestBodyCopyResult, requestBodyException!, requestException,
                    timedOut: requestCancellationSource.IsCancellationRequested);

                try
                {
                    await transformer.TransformResponseAsync(context, proxyResponse: null, requestCancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // We're about to report a more specific error, so ignore OCEs that occur here.
                }

                return error;
            }
        }

        if (requestException is OperationCanceledException)
        {
            Debug.Assert(requestCancellationSource.IsCancellationRequested || requestException.ToString().Contains("ConnectTimeout"), requestException.ToString());

            return await ReportErrorAsync(ForwarderError.RequestTimedOut, StatusCodes.Status504GatewayTimeout);
        }

        // We couldn't communicate with the destination.
        return await ReportErrorAsync(failedDuringRequestCreation ? ForwarderError.RequestCreation : ForwarderError.Request, StatusCodes.Status502BadGateway);

        async ValueTask<ForwarderError> ReportErrorAsync(ForwarderError error, int statusCode)
        {
            ReportProxyError(context, error, requestException);
            context.Response.StatusCode = statusCode;

            if (requestContent is not null && requestContent.InProgress)
            {
                requestCancellationSource.Cancel();
                await requestContent.ConsumptionTask;
            }

            try
            {
                await transformer.TransformResponseAsync(context, proxyResponse: null, requestCancellationSource.Token);
            }
            catch (OperationCanceledException)
            {
                // We may have manually cancelled the request CTS as part of error handling.
                // We're about to report a more specific error, so ignore OCEs that occur here.
            }

            return error;
        }
    }

    private static ValueTask<bool> CopyResponseStatusAndHeadersAsync(HttpResponseMessage source, HttpContext context, HttpTransformer transformer, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)source.StatusCode;

        if (!ProtocolHelper.IsHttp2OrGreater(context.Request.Protocol))
        {
            // Don't explicitly set the field if the default reason phrase is used
            if (source.ReasonPhrase != ReasonPhrases.GetReasonPhrase((int)source.StatusCode))
            {
                context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = source.ReasonPhrase;
            }
        }

        // Copies headers
        return transformer.TransformResponseAsync(context, source, cancellationToken);
    }

    private async ValueTask<ForwarderError> HandleUpgradedResponse(HttpContext context, HttpResponseMessage destinationResponse,
        ActivityCancellationTokenSource activityCancellationSource)
    {
        ForwarderTelemetry.Log.ForwarderStage(ForwarderStage.ResponseUpgrade);

        var isHttp2Request = HttpProtocol.IsHttp2(context.Request.Protocol);
        var headerError = FixupUpgradeResponseHeaders(context, destinationResponse, isHttp2Request);
        if (headerError != ForwarderError.None)
        {
            destinationResponse.Dispose();
            return headerError;
        }

        // :: Step 7-A-1: Upgrade the client channel. This will also send response headers.
        Stream upgradeResult;
        try
        {
#if NET7_0_OR_GREATER
            if (isHttp2Request)
            {
                var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
                Debug.Assert(connectFeature != null);
                upgradeResult = await connectFeature.AcceptAsync();
            }
            else
#endif
            {
                var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
                Debug.Assert(upgradeFeature != null);
                upgradeResult = await upgradeFeature.UpgradeAsync();
            }
#if NET8_0_OR_GREATER
            // Disable request timeout, if there is one, after the upgrade has been accepted
            context.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
#endif
        }
        catch (Exception ex)
        {
            destinationResponse.Dispose();
            ReportProxyError(context, ForwarderError.UpgradeResponseClient, ex);
            return ForwarderError.UpgradeResponseClient;
        }

        using var clientStream = upgradeResult;

        // :: Step 7-A-2: Copy duplex streams
        using var destinationStream = await destinationResponse.Content.ReadAsStreamAsync(activityCancellationSource.Token);

        var requestTask = StreamCopier.CopyAsync(isRequest: true, clientStream, destinationStream, StreamCopier.UnknownLength, _timeProvider, activityCancellationSource,
            // HTTP/2 HttpClient request streams buffer by default.
            autoFlush: destinationResponse.Version == HttpVersion.Version20, activityCancellationSource.Token).AsTask();
        var responseTask = StreamCopier.CopyAsync(isRequest: false, destinationStream, clientStream, StreamCopier.UnknownLength, _timeProvider, activityCancellationSource, activityCancellationSource.Token).AsTask();

        // Make sure we report the first failure.
        var firstTask = await Task.WhenAny(requestTask, responseTask);
        var requestFinishedFirst = firstTask == requestTask;
        var secondTask = requestFinishedFirst ? responseTask : requestTask;

        ForwarderError error;

        var (firstResult, firstException) = await firstTask;
        if (firstResult != StreamCopyResult.Success)
        {
            error = ReportResult(context, requestFinishedFirst, firstResult, firstException, activityCancellationSource);
            // Cancel the other direction
            activityCancellationSource.Cancel();
            // Wait for this to finish before exiting so the resources get cleaned up properly.
            await secondTask;
        }
        else
        {
            var cancelReads = !requestFinishedFirst && !secondTask.IsCompleted;
            if (cancelReads)
            {
                // The response is finished, unblock the incoming reads
                activityCancellationSource.Cancel();
            }

            var (secondResult, secondException) = await secondTask;
            if (!cancelReads && secondResult != StreamCopyResult.Success)
            {
                error = ReportResult(context, !requestFinishedFirst, secondResult, secondException!, activityCancellationSource);
            }
            else
            {
                error = ForwarderError.None;
            }
        }

        return error;

        ForwarderError ReportResult(HttpContext context, bool request, StreamCopyResult result, Exception exception, ActivityCancellationTokenSource activityCancellationTokenSource)
        {
            var error = result switch
            {
                StreamCopyResult.InputError => request ? ForwarderError.UpgradeRequestClient : ForwarderError.UpgradeResponseDestination,
                StreamCopyResult.OutputError => request ? ForwarderError.UpgradeRequestDestination : ForwarderError.UpgradeResponseClient,
                StreamCopyResult.Canceled => request ? ForwarderError.UpgradeRequestCanceled : ForwarderError.UpgradeResponseCanceled,
                _ => throw new NotImplementedException(result.ToString()),
            };
            if (activityCancellationSource.IsCancellationRequested && !activityCancellationSource.CancelledByLinkedToken)
            {
                error = ForwarderError.UpgradeActivityTimeout;
            }
            ReportProxyError(context, error, exception);
            return error;
        }
    }

    // The Connection and Upgrade headers were not copied by default
    private ForwarderError FixupUpgradeResponseHeaders(HttpContext context, HttpResponseMessage response, bool isHttp2Request)
    {
        if (isHttp2Request)
        {
            // H2 <- H1 Validate & remove the Sec-WebSocket-Accept header.
            if (response.Version != HttpVersion.Version20)
            {
                var success = RequestUtilities.TryGetValues(response.RequestMessage!.Headers, HeaderNames.SecWebSocketKey, out var key);
                Debug.Assert(success);
                var accept = context.Response.Headers[HeaderNames.SecWebSocketAccept];
                var expectedAccept = ProtocolHelper.CreateSecWebSocketAccept(key.ToString());
                if (!string.Equals(expectedAccept, accept, StringComparison.Ordinal)) // Base64 is case-sensitive
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    ReportProxyError(context, ForwarderError.ResponseHeaders, new InvalidOperationException("The Sec-WebSocket-Accept header does not match the expected value."));
                    return ForwarderError.ResponseHeaders;
                }
                context.Response.Headers.Remove(HeaderNames.SecWebSocketAccept);
                context.Response.StatusCode = StatusCodes.Status200OK;
            }
            // else H2 <- H2, no changes needed
            return ForwarderError.None;
        }

        // H1 <- H2
        if (response.Version == HttpVersion.Version20)
        {
            // Generate and add the Sec-WebSocket-Accept header, and the Connection and Upgrade headers
            var key = context.Request.Headers[HeaderNames.SecWebSocketKey];
            var accept = ProtocolHelper.CreateSecWebSocketAccept(key);
            context.Response.Headers.TryAdd(HeaderNames.SecWebSocketAccept, accept);
            context.Response.Headers.TryAdd(HeaderNames.Connection, HeaderNames.Upgrade);
            context.Response.Headers.TryAdd(HeaderNames.Upgrade, WebSocketName);
            return ForwarderError.None;
        }

        // H1 <- H1
        // Restore the Connection and Upgrade headers
        // We don't use NonValidated for the Connection header as we do want value validation.
        // HttpHeaders.TryGetValues will handle the parsing and split the values for us.
        if (RequestUtilities.TryGetValues(response.Headers, HeaderNames.Upgrade, out var upgradeValues)
            && response.Headers.TryGetValues(HeaderNames.Connection, out var connectionValues))
        {
            foreach (var value in connectionValues)
            {
                if (value.Equals(HeaderNames.Upgrade, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers.TryAdd(HeaderNames.Connection, value);
                    context.Response.Headers.TryAdd(HeaderNames.Upgrade, upgradeValues);
                    break;
                }
            }
        }

        return ForwarderError.None;
    }

    private async ValueTask<ForwarderError> HandleResponseBodyErrorAsync(HttpContext context, StreamCopyHttpContent? requestContent, StreamCopyResult responseBodyCopyResult, Exception responseBodyException, ActivityCancellationTokenSource requestCancellationSource)
    {
        if (requestContent is not null && requestContent.Started)
        {
            var alreadyFinished = requestContent.ConsumptionTask.IsCompleted;

            if (!alreadyFinished)
            {
                requestCancellationSource.Cancel();
            }

            var (requestBodyCopyResult, requestBodyError) = await requestContent.ConsumptionTask;

            // Check for request body errors, these may have triggered the response error.
            if (alreadyFinished && requestBodyCopyResult is StreamCopyResult.InputError or StreamCopyResult.OutputError)
            {
                return HandleRequestBodyFailure(context, requestBodyCopyResult, requestBodyError!, responseBodyException,
                    timedOut: requestCancellationSource.IsCancellationRequested && !requestCancellationSource.CancelledByLinkedToken);
            }
        }

        var error = responseBodyCopyResult switch
        {
            StreamCopyResult.InputError => ForwarderError.ResponseBodyDestination,
            StreamCopyResult.OutputError => ForwarderError.ResponseBodyClient,
            StreamCopyResult.Canceled => ForwarderError.ResponseBodyCanceled,
            _ => throw new NotImplementedException(responseBodyCopyResult.ToString()),
        };
        ReportProxyError(context, error, responseBodyException);

        if (!context.Response.HasStarted)
        {
            // Nothing has been sent to the client yet, we can still send a good error response.
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return error;
        }

        // The response has already started, we must forcefully terminate it so the client doesn't get
        // the mistaken impression that the truncated response is complete.
        ResetOrAbort(context, isCancelled: responseBodyCopyResult == StreamCopyResult.Canceled);

        return error;
    }

    private static ValueTask CopyResponseTrailingHeadersAsync(HttpResponseMessage source, HttpContext context, HttpTransformer transformer, CancellationToken cancellationToken)
    {
        // Copies trailers
        return transformer.TransformResponseTrailersAsync(context, source, cancellationToken);
    }

    private void ReportProxyError(HttpContext context, ForwarderError error, Exception ex)
    {
        context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(error, ex));
        Log.ErrorProxying(_logger, error, ex);
        ForwarderTelemetry.Log.ForwarderFailed(error);
    }

    private static void ResetOrAbort(HttpContext context, bool isCancelled)
    {
        var resetFeature = context.Features.Get<IHttpResetFeature>();
        if (resetFeature is not null)
        {
            // https://tools.ietf.org/html/rfc7540#section-7
            const int Cancelled = 2;
            const int InternalError = 8;
            resetFeature.Reset(isCancelled ? Cancelled : InternalError);
            return;
        }

        context.Abort();
    }

    private static class Log
    {
        private static readonly Action<ILogger, Version, int, Exception?> _responseReceived = LoggerMessage.Define<Version, int>(
            LogLevel.Information,
            EventIds.ResponseReceived,
            "Received HTTP/{version} response {statusCode}.");

        private static readonly Action<ILogger, string, string, string, string, Exception?> _proxying = LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            EventIds.Forwarding,
            "Proxying to {targetUrl} {version} {versionPolicy} {isStreaming}");

        private static readonly Action<ILogger, ForwarderError, string, Exception> _proxyError = LoggerMessage.Define<ForwarderError, string>(
            LogLevel.Warning,
            EventIds.ForwardingError,
            "{error}: {message}");

        private static readonly Action<ILogger, ForwarderError, string, Exception> _proxyRequestCancelled = LoggerMessage.Define<ForwarderError, string>(
            LogLevel.Debug,
            EventIds.ForwardingRequestCancelled,
            "{error}: {message}");

        private static readonly Action<ILogger, int, Exception?> _notProxying = LoggerMessage.Define<int>(
            LogLevel.Information,
            EventIds.NotForwarding,
            "Not Proxying, a {statusCode} response was set by the transforms.");

        private static readonly Action<ILogger, Exception?> _retryingWebSocketDowngradeNoConnect = LoggerMessage.Define(
            LogLevel.Information,
            EventIds.RetryingWebSocketDowngradeNoConnect,
            "Unable to proxy the WebSocket using HTTP/2, the server does not support RFC 8441, retrying with HTTP/1.1.");

        private static readonly Action<ILogger, Exception?> _retryingWebSocketDowngradeNoHttp2 = LoggerMessage.Define(
            LogLevel.Information,
            EventIds.RetryingWebSocketDowngradeNoHttp2,
            "Unable to proxy the WebSocket using HTTP/2, server does not support HTTP/2. Retrying with HTTP/1.1. Disable HTTP/2 negotiation for improved performance.");

        private static readonly Action<ILogger, string?, Exception?> _invalidKeyHeader = LoggerMessage.Define<string?>(
            LogLevel.Warning,
            EventIds.InvalidSecWebSocketKeyHeader,
            "Invalid Sec-WebSocket-Key header: '{key}'.");

        public static void ResponseReceived(ILogger logger, HttpResponseMessage msg)
        {
            _responseReceived(logger, msg.Version, (int)msg.StatusCode, null);
        }

        public static void Proxying(ILogger logger, HttpRequestMessage msg, bool isStreamingRequest)
        {
            // Avoid computing the AbsoluteUri unless logging is enabled
            if (logger.IsEnabled(LogLevel.Information))
            {
                var streaming = isStreamingRequest ? "streaming" : string.Empty;
                var version = HttpProtocol.GetHttpProtocol(msg.Version);
                var versionPolicy = ProtocolHelper.GetVersionPolicy(msg.VersionPolicy);
                _proxying(logger, msg.RequestUri!.AbsoluteUri, version, versionPolicy, streaming, null);
            }
        }

        public static void NotProxying(ILogger logger, int statusCode)
        {
            _notProxying(logger, statusCode, null);
        }

        public static void InvalidSecWebSocketKeyHeader(ILogger logger, string? key)
        {
            _invalidKeyHeader(logger, key, null);
        }

        public static void ErrorProxying(ILogger logger, ForwarderError error, Exception ex)
        {
            var message = GetMessage(error);

            if (error is
                ForwarderError.RequestCanceled or
                ForwarderError.RequestBodyCanceled or
                ForwarderError.UpgradeRequestCanceled)
            {
                // These error conditions are triggered by the client and are not generally indicative of a problem with the proxy.
                // It's unlikely that they will be useful in most cases, so we log them at Debug level to reduce noise.
                _proxyRequestCancelled(logger, error, message, ex);
            }
            else
            {
                _proxyError(logger, error, message, ex);
            }
        }

        public static void RetryingWebSocketDowngradeNoConnect(ILogger logger)
        {
            _retryingWebSocketDowngradeNoConnect(logger, null);
        }

        public static void RetryingWebSocketDowngradeNoHttp2(ILogger logger)
        {
            _retryingWebSocketDowngradeNoHttp2(logger, null);
        }

        private static string GetMessage(ForwarderError error)
        {
            return error switch
            {
                ForwarderError.None => throw new NotSupportedException("A more specific error must be used"),
                ForwarderError.Request => "An error was encountered before receiving a response.",
                ForwarderError.RequestCreation => "An error was encountered while creating the request message.",
                ForwarderError.RequestTimedOut => "The request timed out before receiving a response.",
                ForwarderError.RequestCanceled => "The request was canceled before receiving a response.",
                ForwarderError.RequestBodyCanceled => "Copying the request body was canceled.",
                ForwarderError.RequestBodyClient => "The client reported an error when copying the request body.",
                ForwarderError.RequestBodyDestination => "The destination reported an error when copying the request body.",
                ForwarderError.ResponseBodyCanceled => "Copying the response body was canceled.",
                ForwarderError.ResponseBodyClient => "The client reported an error when copying the response body.",
                ForwarderError.ResponseBodyDestination => "The destination reported an error when copying the response body.",
                ForwarderError.ResponseHeaders => "The destination returned a response that cannot be proxied back to the client.",
                ForwarderError.UpgradeRequestCanceled => "Copying the upgraded request body was canceled.",
                ForwarderError.UpgradeRequestClient => "The client reported an error when copying the upgraded request body.",
                ForwarderError.UpgradeRequestDestination => "The destination reported an error when copying the upgraded request body.",
                ForwarderError.UpgradeResponseCanceled => "Copying the upgraded response body was canceled.",
                ForwarderError.UpgradeResponseClient => "The client reported an error when copying the upgraded response body.",
                ForwarderError.UpgradeResponseDestination => "The destination reported an error when copying the upgraded response body.",
                ForwarderError.UpgradeActivityTimeout => "The WebSocket connection was closed after being idle longer than the Activity Timeout.",
                ForwarderError.NoAvailableDestinations => throw new NotImplementedException(), // Not used in this class
                _ => throw new NotImplementedException(error.ToString()),
            };
        }
    }
}
