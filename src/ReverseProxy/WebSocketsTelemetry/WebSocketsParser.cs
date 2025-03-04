// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal unsafe struct WebSocketsParser
{
    private const int MaskLength = 4;
    private const int MinHeaderSize = 2;
    private const int MaxHeaderSize = MinHeaderSize + MaskLength + sizeof(ulong);

    private fixed byte _leftoverBuffer[MaxHeaderSize - 1];
    private readonly byte _minHeaderSize;
    private byte _leftover;
    private ulong _bytesToSkip;
    private long _closeTime;
    private readonly TimeProvider _timeProvider;

    public long MessageCount { get; private set; }

    public DateTime? CloseTime => _closeTime == 0 ? null : new DateTime(_closeTime, DateTimeKind.Utc);

    public WebSocketsParser(TimeProvider timeProvider, bool isServer)
    {
        _minHeaderSize = (byte)(MinHeaderSize + (isServer ? MaskLength : 0));
        _leftover = 0;
        _bytesToSkip = 0;
        _closeTime = 0;
        _timeProvider = timeProvider;
        MessageCount = 0;
    }

    // The WebSocket Protocol: https://datatracker.ietf.org/doc/html/rfc6455#section-5.2
    //  0                   1                   2                   3
    //  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    // +-+-+-+-+-------+-+-------------+-------------------------------+
    // |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
    // |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
    // |N|V|V|V|       |S|             |   (if payload len==126/127)   |
    // | |1|2|3|       |K|             |                               |
    // +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
    // |     Extended payload length continued, if payload len == 127  |
    // + - - - - - - - - - - - - - - - +-------------------------------+
    // |                               |Masking-key, if MASK set to 1  |
    // +-------------------------------+-------------------------------+
    // | Masking-key (continued)       |          Payload Data         |
    // +-------------------------------- - - - - - - - - - - - - - - - +
    // :                     Payload Data continued ...                :
    // +---------------------------------------------------------------+
    //
    // The header can be 2-10 bytes long, followed by a 4 byte mask if the message was sent by the client.
    // We have to read the first 2 bytes to know how long the frame header will be.
    // Since the buffer may not contain the full frame, we make use of a leftoverBuffer
    // where we store leftover bytes that don't represent a complete frame header.
    // On the next call to Consume, we interpret the leftover bytes as the beginning of the frame.
    // As we are not interested in the actual payload data, we skip over (payload length + mask length) bytes after each header.
    public void Consume(ReadOnlySpan<byte> buffer)
    {
        int leftover = _leftover;
        var bytesToSkip = _bytesToSkip;

        while (true)
        {
            var toSkip = Math.Min(bytesToSkip, (ulong)buffer.Length);
            buffer = buffer.Slice((int)toSkip);
            bytesToSkip -= toSkip;

            var available = leftover + buffer.Length;
            int headerSize = _minHeaderSize;

            if (available < headerSize)
            {
                break;
            }

            var length = (leftover > 1 ? _leftoverBuffer[1] : buffer[1 - leftover]) & 0x7FUL;

            if (length > 125)
            {
                // The actual length will be encoded in 2 or 8 bytes, based on whether the length was 126 or 127
                var lengthBytes = 2 << (((int)length & 1) << 1);
                headerSize += lengthBytes;
                Debug.Assert(leftover < headerSize);

                if (available < headerSize)
                {
                    break;
                }

                lengthBytes += MinHeaderSize;

                length = 0;
                for (var i = MinHeaderSize; i < lengthBytes; i++)
                {
                    length <<= 8;
                    length |= i < leftover ? _leftoverBuffer[i] : buffer[i - leftover];
                }
            }

            Debug.Assert(leftover < headerSize);
            bytesToSkip = length;

            const int NonReservedBitsMask = 0b_1000_1111;
            var header = (leftover > 0 ? _leftoverBuffer[0] : buffer[0]) & NonReservedBitsMask;

            // Don't count control frames under MessageCount
            if ((uint)(header - 0x80) <= 0x02)
            {
                // Has FIN (0x80) and is a Continuation (0x00) / Text (0x01) / Binary (0x02) opcode
                MessageCount++;
            }
            else if ((header & 0xF) == 0x8) // CLOSE
            {
                if (_closeTime == 0)
                {
                    _closeTime = _timeProvider.GetUtcNow().Ticks;
                }
            }

            // Advance the buffer by the number of bytes read for the header,
            // accounting for any bytes we may have read from the leftoverBuffer
            buffer = buffer.Slice(headerSize - leftover);
            leftover = 0;
        }

        Debug.Assert(bytesToSkip == 0 || buffer.Length == 0);
        _bytesToSkip = bytesToSkip;

        Debug.Assert(leftover + buffer.Length < MaxHeaderSize);
        for (var i = 0; i < buffer.Length; i++, leftover++)
        {
            _leftoverBuffer[leftover] = buffer[i];
        }

        _leftover = (byte)leftover;
    }
}
