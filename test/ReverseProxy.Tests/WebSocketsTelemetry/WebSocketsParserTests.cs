// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using Xunit;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.WebSocketsTelemetry.Tests;

public abstract class WebSocketsParserTests
{
    protected abstract bool IsServer { get; }

    private int MaskSize => IsServer ? 4 : 0;

    private WebSocketsParser CreateParser(TimeProvider timeProvider = null) => new(timeProvider ?? TimeProvider.System, IsServer);

    private ReadOnlySpan<byte> GetHeader(int opcode, int length, bool endOfMessage = true)
    {
        var header = new byte[2 + MaskSize + (length < 126 ? 0 : (length < 65536 ? 2 : 8))];

        Assert.InRange(opcode, 0, 15);
        header[0] = (byte)opcode;

        if (endOfMessage)
        {
            header[0] |= 0x80;
        }

        if (length < 126)
        {
            header[1] = (byte)length;
        }
        else
        {
            header[1] = (byte)(length < 65536 ? 126 : 127);
            var i = header.Length - MaskSize - 1;
            while (length != 0)
            {
                header[i--] = (byte)(length % 256);
                length /= 256;
            }
        }

        if (IsServer)
        {
            header[1] |= 0x80;
        }

        return header;
    }

    private ReadOnlySpan<byte> GetCloseFrame(int length = 0) => GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('a', length)), opcode: 8);

    private ReadOnlySpan<byte> GetPingFrame(int length = 0) => GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('a', length)), opcode: 9);

    private ReadOnlySpan<byte> GetPongFrame(int length = 0) => GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('a', length)), opcode: 10);

    private ReadOnlySpan<byte> GetTextMessageFrame(string message, bool continuation = false, bool endOfMessage = true)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var header = GetHeader(opcode: continuation ? 0 : 1, length: messageBytes.Length, endOfMessage);

        var frame = new byte[header.Length + messageBytes.Length];
        header.CopyTo(frame);
        messageBytes.CopyTo(frame, header.Length);

        return frame;
    }

    private ReadOnlySpan<byte> GetBinaryMessageFrame(ReadOnlySpan<byte> message, bool continuation = false, bool endOfMessage = true, int opcode = 2)
    {
        var header = GetHeader(opcode: continuation ? 0 : opcode, length: message.Length, endOfMessage);

        var frame = new byte[header.Length + message.Length];
        header.CopyTo(frame);
        message.CopyTo(frame.AsSpan(header.Length));

        return frame;
    }

    [Fact]
    public void CustomClockIsUsedForCloseTime()
    {
        var timeProvider = new TestTimeProvider(new TimeSpan(42));
        var parser = CreateParser(timeProvider);

        Assert.Null(parser.CloseTime);

        parser.Consume(GetCloseFrame());

        Assert.NotNull(parser.CloseTime);
        Assert.Equal(timeProvider.GetUtcNow(), parser.CloseTime.Value);
    }

    [Fact]
    public void MessagesAreCountedCorrectly()
    {
        var parser = CreateParser();

        // Whole messages
        parser.Consume(GetTextMessageFrame("Foo"));
        Assert.Equal(1, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(new byte[] { 4, 2 }));
        Assert.Equal(2, parser.MessageCount);


        // Continuations
        parser.Consume(GetTextMessageFrame("Hello, ", endOfMessage: false));
        Assert.Equal(2, parser.MessageCount);

        parser.Consume(GetTextMessageFrame("world", continuation: true, endOfMessage: false));
        Assert.Equal(2, parser.MessageCount);

        parser.Consume(GetTextMessageFrame("!", continuation: true, endOfMessage: true));
        Assert.Equal(3, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(new byte[] { 4 }, endOfMessage: false));
        Assert.Equal(3, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(new byte[] { 2 }, continuation: true, endOfMessage: true));
        Assert.Equal(4, parser.MessageCount);


        // Large messages
        parser.Consume(GetTextMessageFrame(new string('a', 1_000)));
        Assert.Equal(5, parser.MessageCount);

        parser.Consume(GetTextMessageFrame(new string('b', 100_000)));
        Assert.Equal(6, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('c', 1_000))));
        Assert.Equal(7, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('d', 100_000))));
        Assert.Equal(8, parser.MessageCount);


        // Large messages with continuations
        parser.Consume(GetTextMessageFrame(new string('a', 1_000), endOfMessage: false));
        Assert.Equal(8, parser.MessageCount);

        parser.Consume(GetTextMessageFrame(new string('b', 1_000), continuation: true, endOfMessage: true));
        Assert.Equal(9, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('c', 1_000)), endOfMessage: false));
        Assert.Equal(9, parser.MessageCount);

        parser.Consume(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('d', 1_000)), continuation: true, endOfMessage: true));
        Assert.Equal(10, parser.MessageCount);


        // Fragmented frames
        parser.Consume(Array.Empty<byte>());
        Assert.Equal(10, parser.MessageCount);

        ConsumeInFragments(ref parser, GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('a', 1_000))));
        Assert.Equal(11, parser.MessageCount);

        var ms = new MemoryStream();
        for (var i = (int)parser.MessageCount; i < 500; i++)
        {
            // Control frames are not counted
            if (i % 7 == 0)
            {
                ms.Write(GetPingFrame());
            }
            if (i % 13 == 0)
            {
                ms.Write(GetPongFrame());
            }

            switch (i % 4)
            {
                case 0:
                    ms.Write(GetTextMessageFrame(new string('a', i)));
                    break;

                case 1:
                    ms.Write(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('b', i))));
                    break;

                case 2:
                    ms.Write(GetTextMessageFrame(new string('a', i), endOfMessage: false));
                    ms.Write(GetTextMessageFrame(new string('b', i), continuation: true, endOfMessage: false));
                    ms.Write(GetTextMessageFrame(new string('c', i), continuation: true, endOfMessage: true));
                    break;

                case 3:
                    ms.Write(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('a', i)), endOfMessage: false));
                    ms.Write(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('b', i)), continuation: true, endOfMessage: false));
                    ms.Write(GetBinaryMessageFrame(Encoding.UTF8.GetBytes(new string('c', i)), continuation: true, endOfMessage: true));
                    break;
            }
        }
        ConsumeInFragments(ref parser, ms.ToArray());
        Assert.Equal(500, parser.MessageCount);


        // Control frames are not counted
        parser.Consume(GetPingFrame());
        parser.Consume(GetPingFrame(length: 10));
        parser.Consume(GetPongFrame());
        parser.Consume(GetPongFrame(length: 10));
        parser.Consume(GetCloseFrame());
        parser.Consume(GetCloseFrame(length: 10));
        Assert.Equal(500, parser.MessageCount);


        // Messages are still counted after a close frame
        parser.Consume(GetTextMessageFrame("Foo"));
        Assert.Equal(501, parser.MessageCount);

        static void ConsumeInFragments(ref WebSocketsParser parser, ReadOnlySpan<byte> message)
        {
            var rng = new Random(42);
            while (message.Length != 0)
            {
                var fragmentLength = Math.Min(message.Length, rng.Next(0, 150));
                parser.Consume(message[..fragmentLength]);
                message = message[fragmentLength..];
            }
        }
    }
}

public sealed class WebSocketsParserTests_Client : WebSocketsParserTests
{
    protected override bool IsServer => false;
}

public sealed class WebSocketsParserTests_Server : WebSocketsParserTests
{
    protected override bool IsServer => true;
}
