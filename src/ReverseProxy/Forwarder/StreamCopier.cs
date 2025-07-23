// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// A stream copier that captures errors.
/// </summary>
internal static class StreamCopier
{
    // Based on performance investigations, see https://github.com/dotnet/yarp/pull/330#issuecomment-758851852.
    private const int DefaultBufferSize = 65536;
    public const long UnknownLength = -1;

    public static ValueTask<(StreamCopyResult, Exception?)> CopyAsync(bool isRequest, Stream input, Stream output, long promisedContentLength, TimeProvider timeProvider, ActivityCancellationTokenSource activityToken, CancellationToken cancellation)
        => CopyAsync(isRequest, input, output, promisedContentLength, timeProvider, activityToken, autoFlush: false, cancellation);

    public static ValueTask<(StreamCopyResult, Exception?)> CopyAsync(bool isRequest, Stream input, Stream output, long promisedContentLength, TimeProvider timeProvider, ActivityCancellationTokenSource activityToken, bool autoFlush, CancellationToken cancellation)
    {
        Debug.Assert(input is not null);
        Debug.Assert(output is not null);
        Debug.Assert(timeProvider is not null);
        Debug.Assert(activityToken is not null);

        // Avoid capturing 'isRequest' and 'timeProvider' in the state machine when telemetry is disabled
        var telemetry = ForwarderTelemetry.Log.IsEnabled(EventLevel.Informational, EventKeywords.All)
            ? new StreamCopierTelemetry(isRequest, timeProvider)
            : null;

        return CopyAsync(input, output, promisedContentLength, telemetry, activityToken, autoFlush, cancellation);
    }

    private static async ValueTask<(StreamCopyResult, Exception?)> CopyAsync(Stream input, Stream output, long promisedContentLength, StreamCopierTelemetry? telemetry, ActivityCancellationTokenSource activityToken, bool autoFlush, CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        var read = 0;
        long contentLength = 0;
        try
        {
            while (true)
            {
                read = 0;

                // Issue a zero-byte read to the input stream to defer buffer allocation until data is available.
                // Note that if the underlying stream does not supporting blocking on zero byte reads, then this will
                // complete immediately and won't save any memory, but will still function correctly.
                var zeroByteReadTask = input.ReadAsync(Memory<byte>.Empty, cancellation);
                if (zeroByteReadTask.IsCompletedSuccessfully)
                {
                    // Consume the ValueTask's result in case it is backed by an IValueTaskSource
                    _ = zeroByteReadTask.Result;
                }
                else
                {
                    // Take care not to return the same buffer to the pool twice in case zeroByteReadTask throws
                    var bufferToReturn = buffer;
                    buffer = null;
                    ArrayPool<byte>.Shared.Return(bufferToReturn);

                    await zeroByteReadTask;

                    buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                }

                read = await input.ReadAsync(buffer.AsMemory(), cancellation);
                contentLength += read;
                // Normally this is enforced by the server, but it could get out of sync if something in the proxy modified the body.
                if (promisedContentLength != UnknownLength && contentLength > promisedContentLength)
                {
                    return (StreamCopyResult.InputError, new InvalidOperationException("More bytes received than the specified Content-Length."));
                }

                telemetry?.AfterRead(contentLength);

                // Success, reset the activity monitor.
                activityToken.ResetTimeout();

                // End of the source stream.
                if (read == 0)
                {
                    if (promisedContentLength == UnknownLength || contentLength == promisedContentLength)
                    {
                        return (StreamCopyResult.Success, null);
                    }
                    else
                    {
                        // This can happen if something in the proxy consumes or modifies part or all of the request body before proxying.
                        return (StreamCopyResult.InputError,
                            new InvalidOperationException($"Sent {contentLength} request content bytes, but Content-Length promised {promisedContentLength}."));
                    }
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                if (autoFlush)
                {
                    // HttpClient doesn't always flush outgoing data unless the buffer is full or the caller asks.
                    // This is a problem for streaming protocols like WebSockets and gRPC.
                    await output.FlushAsync(cancellation);
                }

                telemetry?.AfterWrite();

                // Success, reset the activity monitor.
                activityToken.ResetTimeout();
            }
        }
        catch (Exception ex)
        {
            if (read == 0)
            {
                telemetry?.AfterRead(contentLength);
            }
            else
            {
                telemetry?.AfterWrite();
            }

            if (activityToken.CancelledByLinkedToken)
            {
                return (StreamCopyResult.Canceled, ex);
            }

            // If the activity timeout triggered while reading or writing, blame the sender or receiver.
            var result = read == 0 ? StreamCopyResult.InputError : StreamCopyResult.OutputError;
            return (result, ex);
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            telemetry?.Stop();
        }
    }

    private sealed class StreamCopierTelemetry
    {
        private readonly bool _isRequest;
        private readonly TimeProvider _timeProvider;
        private long _contentLength;
        private long _iops;
        private long _readTime;
        private long _writeTime;
        private long _firstReadTime;
        private long _lastTime;
        private long _nextTransferringEvent;

        public StreamCopierTelemetry(bool isRequest, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);

            _isRequest = isRequest;
            _timeProvider = timeProvider;
            _firstReadTime = -1;

            ForwarderTelemetry.Log.ForwarderStage(isRequest ? ForwarderStage.RequestContentTransferStart : ForwarderStage.ResponseContentTransferStart);

            _lastTime = timeProvider.GetTimestamp();
            _nextTransferringEvent = _lastTime + _timeProvider.TimestampFrequency;
        }

        public void AfterRead(long contentLength)
        {
            _contentLength = contentLength;
            _iops++;

            var readStop = _timeProvider.GetTimestamp();
            var currentReadTime = readStop - _lastTime;
            _lastTime = readStop;
            _readTime += currentReadTime;
            if (_firstReadTime < 0)
            {
                _firstReadTime = currentReadTime;
            }
        }

        public void AfterWrite()
        {
            var writeStop = _timeProvider.GetTimestamp();
            _writeTime += writeStop - _lastTime;
            _lastTime = writeStop;

            if (writeStop >= _nextTransferringEvent)
            {
                ForwarderTelemetry.Log.ContentTransferring(
                    _isRequest,
                    _contentLength,
                    _iops,
                    _timeProvider.GetElapsedTime(0, _readTime).Ticks,
                    _timeProvider.GetElapsedTime(0, _writeTime).Ticks);

                // Avoid attributing the time taken by logging ContentTransferring to the next read call
                _lastTime = _timeProvider.GetTimestamp();
                _nextTransferringEvent = _lastTime + _timeProvider.TimestampFrequency;
            }
        }

        public void Stop()
        {
            ForwarderTelemetry.Log.ContentTransferred(
                _isRequest,
                _contentLength,
                _iops,
                _timeProvider.GetElapsedTime(0, _readTime).Ticks,
                _timeProvider.GetElapsedTime(0, _writeTime).Ticks,
                _timeProvider.GetElapsedTime(0, Math.Max(0, _firstReadTime)).Ticks);
        }
    }
}
