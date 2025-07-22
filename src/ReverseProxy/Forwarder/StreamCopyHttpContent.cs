// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Custom <see cref="HttpContent"/>
/// used to proxy the incoming request body to the upstream server.
/// </summary>
/// <remarks>
/// <para>
/// By implementing a custom <see cref="HttpContent"/>, we are able to execute
/// our custom code for all HTTP protocol versions.
/// See the remarks section of <see cref="SerializeToStreamAsync(Stream, TransportContext)"/>
/// for more details.
/// </para>
/// <para>
/// <see cref="HttpContent"/> declares an internal property `AllowDuplex`
/// which, when set to true, causes <see cref="HttpClient"/> and friends
/// to NOT tie up the request body stream operations to the same cancellation token
/// that is passed to <see cref="HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>.
/// </para>
/// <para>
/// When proxying duplex channels such as HTTP/2, gRPC,
/// we need `HttpContent.AllowDuplex` to be true.
/// It so happens to be by default on .NET Core 3.1. Should that ever change,
/// this class will need to be updated.
/// </para>
/// </remarks>
internal sealed class StreamCopyHttpContent : HttpContent
{
    private readonly HttpContext _context;
    // HttpClient's machinery keeps an internal buffer that doesn't get flushed to the socket on every write.
    // Some protocols (e.g. gRPC) may rely on specific bytes being sent, and HttpClient's buffering would prevent it.
    private bool _isStreamingRequest;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly ActivityCancellationTokenSource _activityToken;
    private readonly TaskCompletionSource<(StreamCopyResult, Exception?)> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    public StreamCopyHttpContent(HttpContext context, bool isStreamingRequest, TimeProvider timeProvider, ILogger logger, ActivityCancellationTokenSource activityToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _context = context;
        _isStreamingRequest = isStreamingRequest;
        _timeProvider = timeProvider;
        _logger = logger;
        _activityToken = activityToken;
    }

    /// <summary>
    /// Gets a <see cref="Task"/> that completes in successful or failed state
    /// mimicking the result of SerializeToStreamAsync.
    /// </summary>
    public Task<(StreamCopyResult, Exception?)> ConsumptionTask => _tcs.Task;

    /// <summary>
    /// Gets a value indicating whether consumption of this content has begun.
    /// Property <see cref="ConsumptionTask"/> can be used to track the asynchronous outcome of the operation.
    /// </summary>
    /// <remarks>
    /// When used as an outgoing request content with <see cref="HttpClient"/>,
    /// this should always be true by the time the task returned by
    /// <see cref="HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>
    /// completes, even when using <see cref="HttpCompletionOption.ResponseHeadersRead"/>.
    /// </remarks>
    public bool Started => Volatile.Read(ref _started) == 1;

    public bool InProgress => Started && !ConsumptionTask.IsCompleted;

    /// <summary>
    /// Copies bytes from the stream provided in our constructor into the target <paramref name="stream"/>.
    /// </summary>
    /// <remarks>
    /// This is used internally by HttpClient.SendAsync to send the request body.
    /// Here's the sequence of events as of commit 17300169760c61a90cab8d913636c1058a30a8c1 (https://github.com/dotnet/corefx -- tag v3.1.1).
    ///
    /// <code>
    /// HttpClient.SendAsync -->
    /// HttpMessageInvoker.SendAsync -->
    /// HttpClientHandler.SendAsync -->
    /// SocketsHttpHandler.SendAsync -->
    /// HttpConnectionHandler.SendAsync -->
    /// HttpConnectionPoolManager.SendAsync -->
    /// HttpConnectionPool.SendAsync --> ... -->
    /// {
    ///     HTTP/1.1: HttpConnection.SendAsync -->
    ///               HttpConnection.SendAsyncCore -->
    ///               HttpConnection.SendRequestContentAsync -->
    ///               HttpContent.CopyToAsync
    ///
    ///     HTTP/2:   Http2Connection.SendAsync -->
    ///               Http2Stream.SendRequestBodyAsync -->
    ///               HttpContent.CopyToAsync
    ///
    ///     /* Only in .NET 5:
    ///     HTTP/3:   Http3Connection.SendAsync -->
    ///               Http3Connection.SendWithoutWaitingAsync -->
    ///               Http3RequestStream.SendAsync -->
    ///               Http3RequestStream.SendContentAsync -->
    ///               HttpContent.CopyToAsync
    ///     */
    /// }
    ///
    /// HttpContent.CopyToAsync -->
    /// HttpContent.SerializeToStreamAsync (bingo!)
    /// </code>
    ///
    /// Conclusion: by overriding HttpContent.SerializeToStreamAsync,
    /// we have full control over pumping bytes to the target stream for all protocols
    /// (except Web Sockets, which is handled separately).
    /// </remarks>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw new InvalidOperationException("Stream was already consumed.");
        }

        // The cancellationToken that is passed to this method is:
        // On HTTP/1.1: Linked HttpContext.RequestAborted + Request Timeout
        // On HTTP/2.0: SocketsHttpHandler error / the server wants us to stop sending content / H2 connection closed
        // _cancellation will be the same as cancellationToken for HTTP/1.1, so we can avoid the overhead of linking them
        CancellationTokenSource? linkedCts = null;

        if (_activityToken.Token == cancellationToken)
        {
            // We're talking to the destination via HTTP/1.1, so this can't be a streaming gRPC request.
            _isStreamingRequest = false;
            // TODO: Log if _isStreamingRequest is true? Something went wrong with protocol selection.
        }
        else
        {
            Debug.Assert(cancellationToken.CanBeCanceled);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_activityToken.Token, cancellationToken);
            cancellationToken = linkedCts.Token;

            if (_isStreamingRequest)
            {
                DisableMinRequestBodyDataRateAndMaxRequestBodySize(_context);
            }
        }

        try
        {
            // Immediately flush request stream to send headers
            // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
            try
            {
                await stream.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                var canceled = ex is OperationCanceledException || _activityToken.CancelledByLinkedToken;
                _tcs.TrySetResult((canceled ? StreamCopyResult.Canceled : StreamCopyResult.OutputError, ex));
                return;
            }

            // Check that the content-length matches the request body size. This can be removed in .NET 7 now that SocketsHttpHandler
            // enforces this: https://github.com/dotnet/runtime/issues/62258.
            //
            // Note on `_isStreamingRequest`:
            // The.NET Core HttpClient stack keeps its own buffers on top of the underlying outgoing connection socket.
            // We flush those buffers down to the socket on every write when this is set,
            // but it does NOT result in calls to flush on the underlying socket.
            // This is necessary because we proxy http2 transparently,
            // and we are deliberately unaware of packet structure used e.g. in gRPC duplex channels.
            // Because the sockets aren't flushed, the perf impact of this choice is expected to be small.
            // Future: It may be wise to set this to true for *all* http2 incoming requests,
            // but for now, out of an abundance of caution, we only do it for requests that look like gRPC.
            var (result, error) = await StreamCopier.CopyAsync(isRequest: true, _context.Request.Body, stream,
                Headers.ContentLength ?? StreamCopier.UnknownLength, _timeProvider, _activityToken, _isStreamingRequest, cancellationToken);
            _tcs.TrySetResult((result, error));

            // Check for errors that weren't the result of the destination failing.
            // We have to throw something here so the transport knows the body is incomplete.
            // We can't re-throw the original exception since that would cause concurrency issues.
            // We need to wrap it.
            if (result == StreamCopyResult.InputError)
            {
                throw new IOException("An error occurred when reading the request body from the client.", error);
            }
            if (result == StreamCopyResult.Canceled)
            {
                throw new OperationCanceledException("The request body copy was canceled.", error);
            }
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    // this is used internally by HttpContent.ReadAsStreamAsync(...)
    protected override Task<Stream> CreateContentReadStreamAsync()
    {
        // Nobody should be calling this...
        throw new NotImplementedException();
    }

    protected override bool TryComputeLength(out long length)
    {
        // We can't know the length of the content being pushed to the output stream.
        length = -1;
        return false;
    }

    /// <summary>
    /// Disable some ASP .NET Core server limits so that we can handle long-running gRPC requests unconstrained.
    /// Note that the gRPC server implementation on ASP .NET Core does the same for client-streaming and duplex methods.
    /// Since in Gateway we have no way to determine if the current request requires client-streaming or duplex comm,
    /// we do this for *all* incoming requests that look like they might be gRPC.
    /// </summary>
    /// <remarks>
    /// Inspired on
    /// <see href="https://github.com/grpc/grpc-dotnet/blob/3ce9b104524a4929f5014c13cd99ba9a1c2431d4/src/Grpc.AspNetCore.Server/Internal/CallHandlers/ServerCallHandlerBase.cs#L127"/>.
    /// </remarks>
    private void DisableMinRequestBodyDataRateAndMaxRequestBodySize(HttpContext httpContext)
    {
        var minRequestBodyDataRateFeature = httpContext.Features.Get<IHttpMinRequestBodyDataRateFeature>();
        if (minRequestBodyDataRateFeature is not null)
        {
            minRequestBodyDataRateFeature.MinDataRate = null;
        }

        var maxRequestBodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature is not null)
        {
            if (!maxRequestBodySizeFeature.IsReadOnly)
            {
                maxRequestBodySizeFeature.MaxRequestBodySize = null;
            }
            else
            {
                // IsReadOnly could be true if middleware has already started reading the request body
                // In that case we can't disable the max request body size for the request stream
                _logger.LogWarning("Unable to disable max request body size.");
            }
        }
    }
}
