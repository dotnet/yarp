// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SampleServer.Controllers;

/// <summary>
/// Sample controller.
/// </summary>
[ApiController]
public class WebSocketsController : ControllerBase
{
    private readonly ILogger<WebSocketsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketsController" /> class.
    /// </summary>
    public WebSocketsController(ILogger<WebSocketsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a 200 response.
    /// </summary>
    [HttpGet]
    [Route("/api/websockets")]
    public async Task WebSockets()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }

        using (var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync())
        {
            _logger.LogInformation("WebSockets established.");
            await RunPingPongAsync(webSocket, HttpContext.RequestAborted);
        }

        _logger.LogInformation("WebSockets finished.");
    }

    private static async Task RunPingPongAsync(WebSocket webSocket, CancellationToken cancellation)
    {
        var buffer = new byte[1024];
        while (true)
        {
            var message = await webSocket.ReceiveAsync(buffer, cancellation);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", cancellation);
                return;
            }

            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, message.Count),
                message.MessageType,
                message.EndOfMessage,
                cancellation);
        }
    }
}
