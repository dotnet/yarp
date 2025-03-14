// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios;

internal sealed class WebSocketsScenario : IScenario
{
    public async Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation)
    {
        var client = new ClientWebSocket();
        client.Options.AddSubProtocol("chat");

        var webSocketsTarget = args.Target.Replace("https://", "wss://").Replace("http://", "ws://");
        var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "api/websockets");
        Console.WriteLine($"Establishing WebSockets channel with {targetUri}...");

        var stopwatch = Stopwatch.StartNew();
        await client.ConnectAsync(targetUri, cancellation);
        Console.WriteLine($"Channel established in {stopwatch.ElapsedMilliseconds} ms.");

        Console.WriteLine("Sending text messages...");
        var buffer = new byte[1024];
        stopwatch.Restart();
        for (var i = 0; i < 256; i++)
        {
            var textToSend = $"Hello {i}";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellation);

            var message = await client.ReceiveAsync(buffer, cancellation);
            if (message.MessageType != WebSocketMessageType.Text)
            {
                throw new Exception($"Expected to receive a text message, got '{message.MessageType}' instead.");
            }

            if (!message.EndOfMessage)
            {
                throw new Exception("Expected to receive EndOfMessage = true.");
            }

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            if (text != textToSend)
            {
                throw new Exception($"Expected to receive '{textToSend}', but got '{text}'.");
            }

            Console.Write(".");
        }

        Console.WriteLine();
        Console.WriteLine($"Completed 256 text messages in {stopwatch.ElapsedMilliseconds} ms.");

        Console.WriteLine("Sending binary messages...");
        stopwatch.Restart();
        for (var i = 0; i < 256; i++)
        {
            var textToSend = $"Hello {i}";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellation);

            var message = await client.ReceiveAsync(buffer, cancellation);
            if (message.MessageType != WebSocketMessageType.Binary)
            {
                throw new Exception($"Expected to receive a text message, got '{message.MessageType}' instead.");
            }

            if (!message.EndOfMessage)
            {
                throw new Exception("Expected to receive EndOfMessage = true.");
            }

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            if (text != textToSend)
            {
                throw new Exception($"Expected to receive '{textToSend}', but got '{text}'.");
            }

            Console.Write(".");
        }

        Console.WriteLine();
        Console.WriteLine($"Completed 256 binary messages in {stopwatch.ElapsedMilliseconds} ms.");

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", cancellation);
    }
}
