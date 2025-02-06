// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal sealed class WebSocketsTelemetryStream : DelegatingStream
{
    private WebSocketsParser _readParser, _writeParser;

    public DateTime EstablishedTime { get; }
    public long MessagesRead => _readParser.MessageCount;
    public long MessagesWritten => _writeParser.MessageCount;

    public WebSocketsTelemetryStream(TimeProvider timeProvider, Stream innerStream)
        : base(innerStream)
    {
        EstablishedTime = timeProvider.GetUtcNow().UtcDateTime;
        _readParser = new WebSocketsParser(timeProvider, isServer: true);
        _writeParser = new WebSocketsParser(timeProvider, isServer: false);
    }

    public WebSocketCloseReason GetCloseReason(HttpContext context)
    {
        var clientCloseTime = _readParser.CloseTime;
        var serverCloseTime = _writeParser.CloseTime;

        // Mutual, graceful WebSocket close. We report whichever one we saw first.
        if (clientCloseTime.HasValue && serverCloseTime.HasValue)
        {
            return clientCloseTime.Value < serverCloseTime.Value ? WebSocketCloseReason.ClientGracefulClose : WebSocketCloseReason.ServerGracefulClose;
        }

        // One side sent a WebSocket close, but we never saw a response from the other side
        // It is possible an error occurred, but we saw a graceful close first, so that is the initiator
        if (clientCloseTime.HasValue)
        {
            return WebSocketCloseReason.ClientGracefulClose;
        }
        if (serverCloseTime.HasValue)
        {
            return WebSocketCloseReason.ServerGracefulClose;
        }

        return context.Features.Get<IForwarderErrorFeature>()?.Error switch
        {
            // Either side disconnected without sending a WebSocket close
            ForwarderError.UpgradeRequestClient => WebSocketCloseReason.ClientDisconnect,
            ForwarderError.UpgradeRequestCanceled => WebSocketCloseReason.ClientDisconnect,
            ForwarderError.UpgradeResponseClient => WebSocketCloseReason.ClientDisconnect,
            ForwarderError.UpgradeResponseCanceled => WebSocketCloseReason.ClientDisconnect,
            ForwarderError.UpgradeRequestDestination => WebSocketCloseReason.ServerDisconnect,
            ForwarderError.UpgradeResponseDestination => WebSocketCloseReason.ServerDisconnect,

            // Activity Timeout
            ForwarderError.UpgradeActivityTimeout => WebSocketCloseReason.ActivityTimeout,

            // Both sides gracefully closed the underlying connection without sending a WebSocket close,
            // or the server closed the connection and we canceled the client and suppressed the errors.
            null => WebSocketCloseReason.ServerDisconnect,

            // We are not expecting any other error from HttpForwarder after a successful connection upgrade
            // Technically, a user could overwrite the IForwarderErrorFeature, in which case we don't know what's going on
            _ => WebSocketCloseReason.Unknown
        };
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var readTask = base.ReadAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return readTask;
        }

        if (readTask.IsCompletedSuccessfully)
        {
            var read = readTask.GetAwaiter().GetResult();
            _readParser.Consume(buffer.Span.Slice(0, read));
            return new ValueTask<int>(read);
        }

        return Core(buffer, readTask);

        async ValueTask<int> Core(Memory<byte> buffer, ValueTask<int> readTask)
        {
            var read = await readTask;
            _readParser.Consume(buffer.Span.Slice(0, read));
            return read;
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _writeParser.Consume(buffer.Span);
        return base.WriteAsync(buffer, cancellationToken);
    }
}
