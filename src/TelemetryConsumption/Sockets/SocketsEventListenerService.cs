// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class SocketsEventListenerService : EventListenerService<SocketsEventListenerService, ISocketsTelemetryConsumer, SocketsMetrics>
{
    protected override string EventSourceName => "System.Net.Sockets";

#if NET8_0_OR_GREATER
    protected override int NumberOfMetrics => 7;
#else
    protected override int NumberOfMetrics => 6;
#endif

    public SocketsEventListenerService(ILogger<SocketsEventListenerService> logger, IEnumerable<ISocketsTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<SocketsMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(ISocketsTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "ConnectStart" && payload.Count == 1);
                {
                    var address = (string)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectStart(eventData.TimeStamp, address);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "ConnectStop" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectStop(eventData.TimeStamp);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "ConnectFailed" && payload.Count == 2);
                {
                    var error = (SocketError)payload[0];
                    var exceptionMessage = (string)payload[1];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectFailed(eventData.TimeStamp, error, exceptionMessage);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(SocketsMetrics metrics, string name, double value)
    {
        var longValue = (long)value;

        switch (name)
        {
            case "outgoing-connections-established":
                metrics.OutgoingConnectionsEstablished = longValue;
                break;

            case "incoming-connections-established":
                metrics.IncomingConnectionsEstablished = longValue;
                break;

            case "bytes-received":
                metrics.BytesReceived = longValue;
                break;

            case "bytes-sent":
                metrics.BytesSent = longValue;
                break;

            case "datagrams-received":
                metrics.DatagramsReceived = longValue;
                break;

            case "datagrams-sent":
                metrics.DatagramsSent = longValue;
                break;

#if NET8_0_OR_GREATER
            case "current-outgoing-connect-attempts":
                metrics.CurrentOutgoingConnectAttempts = longValue;
                break;
#endif

            default:
                return false;
        }

        return true;
    }
}
