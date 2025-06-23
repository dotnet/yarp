// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Utilities.Tls;

namespace Yarp.ReverseProxy.Sample;

public static class TlsFilter
{
    // Use reasonable limits. Parsing across multiple segments has an O(N^2) worst case, so limit the N.
    private const int ClientHelloTimeoutMs = 10_000;
    private const int MaxClientHelloSize = 10 * 1024; // 10 KB

    // This sniffs the TLS handshake and rejects requests that meat specific criteria.
    internal static async Task ProcessAsync(ConnectionContext connectionContext, Func<Task> next, ILogger logger)
    {
        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(connectionContext.ConnectionClosed))
        {
            timeoutCts.CancelAfter(ClientHelloTimeoutMs);

            var input = connectionContext.Transport.Input;

            // Count how many bytes we've examined so we never go backwards, Pipes don't allow that.
            var minBytesExamined = 0L;

            while (true)
            {
                var result = await input.ReadAsync(timeoutCts.Token);
                var buffer = result.Buffer;

                if (result.IsCompleted || result.IsCanceled)
                {
                    return;
                }

                if (buffer.Length == 0)
                {
                    continue;
                }

                if (!TryReadTlsFrame(buffer, logger, out var frameInfo) && frameInfo.ParsingStatus == TlsFrameHelper.ParsingStatus.IncompleteFrame)
                {
                    // We didn't find a TLS frame, we need to read more data.
                    minBytesExamined = buffer.Length;

                    if (minBytesExamined >= MaxClientHelloSize)
                    {
                        logger.LogInformation("Client Hello too large. Aborting.");
                        return;
                    }

                    input.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                // We're done. We either have a frame we can analyze, or we're giving up.
                var examined = buffer.Slice(buffer.Start, minBytesExamined).End;
                input.AdvanceTo(buffer.Start, examined);

                if (frameInfo.ParsingStatus != TlsFrameHelper.ParsingStatus.Ok || frameInfo.HandshakeType != TlsHandshakeType.ClientHello)
                {
                    logger.LogInformation("Invalid or unexpected TLS frame. Aborting.");
                    return;
                }

                // Perform any additional validation on the Client Hello here.
                // Rate limiting, throttling checks, J4A fingerprinting, logging, etc. can be performed here as well.

                if (!TryProcessClientHello(frameInfo, logger))
                {
                    // Abort the connection.
                    return;
                }

                // All checks passed, we can continue processing the request.

#if !NET10_0_OR_GREATER
                // Workaround for https://github.com/dotnet/runtime/issues/107213, which was fixed in .NET 10.
                if (minBytesExamined > 0)
                {
                    connectionContext.Transport = new DuplexPipe(
                        PipeReader.Create(input.AsStream(), new StreamPipeReaderOptions(bufferSize: Math.Max(4096, (int)minBytesExamined))),
                        connectionContext.Transport.Output);
                }
#endif

                break;
            }
        }

        await next();
    }

    /// <summary>Process the Client Hello and returns whether it passed validation.</summary>
    private static bool TryProcessClientHello(TlsFrameHelper.TlsFrameInfo clientHello, ILogger logger)
    {
        // This is a sample demonstrating several checks you can perform on the Client Hello.
        // Replace the logic in this method with your own validation logic.

        string sni = clientHello.TargetName;

        if (string.IsNullOrEmpty(sni))
        {
            logger.LogInformation("Expected SNI to be specified.");
            return false;
        }

        if (!AllowHost(sni))
        {
            logger.LogInformation("Unexpected SNI: {sni}.", sni);
            return false;
        }

        if (!clientHello.SupportedVersions.HasFlag(SslProtocols.Tls12) && !clientHello.SupportedVersions.HasFlag(SslProtocols.Tls13))
        {
            logger.LogInformation("Client for '{sni}' does not support TLS 1.2 or 1.3.", sni);
            return false;
        }

        if (!clientHello.ApplicationProtocols.HasFlag(TlsFrameHelper.ApplicationProtocolInfo.Http2))
        {
            logger.LogInformation("Client for '{sni}' does not support HTTP/2.", sni);
            return false;
        }

        // All checks passed, we can continue processing the request.
        return true;
    }

    private static bool AllowHost(string targetName)
    {
        return
            targetName.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            targetName.Equals("contoso.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Attempt to parse the first TLS frame from the <paramref name="buffer"/> and indicate whether more data is needed.</summary>
    private static bool TryReadTlsFrame(ReadOnlySequence<byte> buffer, ILogger logger, out TlsFrameHelper.TlsFrameInfo frame)
    {
        frame = default;

        // Try to process the first segment first.
        var data = buffer.First.Span;

        if (TlsFrameHelper.TryGetFrameInfo(data, ref frame))
        {
            // This is the common fast path.
            return true;
        }

        if (frame.ParsingStatus != TlsFrameHelper.ParsingStatus.IncompleteFrame)
        {
            // The input is invalid, reading more data won't help.
            return false;
        }

        if (buffer.IsSingleSegment)
        {
            // We only have one segment and it didn't contain a valid TLS frame. We'll have to read more data.
            return false;
        }

        // We have multiple segments. TlsFrameHelper only works with a single span, so we need to combine them.
        // This may happen on every new read, which is why we limit how much data we're willing to process.

        var pooledBuffer = ArrayPool<byte>.Shared.Rent((int)buffer.Length);
        buffer.CopyTo(pooledBuffer);
        data = pooledBuffer.AsSpan(0, (int)buffer.Length);

        bool success = TlsFrameHelper.TryGetFrameInfo(data, ref frame);

        ArrayPool<byte>.Shared.Return(pooledBuffer);

        if (success)
        {
            logger.LogDebug("Parsed multi-segment TLS frame after {length} bytes", buffer.Length);
        }

        return success;
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;
        public PipeWriter Output { get; } = output;
    }
}
