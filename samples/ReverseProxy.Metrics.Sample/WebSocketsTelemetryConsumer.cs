// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Yarp.Telemetry.Consumption;

namespace Yarp.Sample
{
    public sealed class WebSocketsTelemetryConsumer : IWebSocketsTelemetryConsumer
    {
        private readonly ILogger<WebSocketsTelemetryConsumer> _logger;

        public WebSocketsTelemetryConsumer(ILogger<WebSocketsTelemetryConsumer> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        public void OnWebSocketClosed(DateTime timestamp, DateTime establishedTime, WebSocketCloseReason closeReason, long messagesRead, long messagesWritten)
        {
            _logger.LogInformation($"WebSocket connection closed ({closeReason}) after reading {messagesRead} and writing {messagesWritten} messages over {(timestamp - establishedTime).TotalSeconds:N2} seconds.");
        }
    }
}
