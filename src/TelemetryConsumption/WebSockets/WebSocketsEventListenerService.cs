// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class WebSocketsMetrics { }

internal sealed class WebSocketsEventListenerService : EventListenerService<WebSocketsEventListenerService, IWebSocketsTelemetryConsumer, WebSocketsMetrics>
{
    protected override string EventSourceName => "Yarp.ReverseProxy.WebSockets";

    protected override int NumberOfMetrics => 0;

    public WebSocketsEventListenerService(ILogger<WebSocketsEventListenerService> logger, IEnumerable<IWebSocketsTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<WebSocketsMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(IWebSocketsTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "WebSocketClosed" && payload.Count == 4);
                {
                    var establishedTime = new DateTime((long)payload[0]);
                    var closeReason = (WebSocketCloseReason)payload[1];
                    var messagesRead = (long)payload[2];
                    var messagesWritten = (long)payload[3];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnWebSocketClosed(eventData.TimeStamp, establishedTime, closeReason, messagesRead, messagesWritten);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(WebSocketsMetrics metrics, string name, double value)
    {
        return false;
    }
}
