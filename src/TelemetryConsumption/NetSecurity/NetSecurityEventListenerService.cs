// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class NetSecurityEventListenerService : EventListenerService<NetSecurityEventListenerService, INetSecurityTelemetryConsumer, NetSecurityMetrics>
{
    protected override string EventSourceName => "System.Net.Security";

    protected override int NumberOfMetrics => 14;

    public NetSecurityEventListenerService(ILogger<NetSecurityEventListenerService> logger, IEnumerable<INetSecurityTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<NetSecurityMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(INetSecurityTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "HandshakeStart" && payload.Count == 2);
                {
                    var isServer = (bool)payload[0];
                    var targetHost = (string)payload[1];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnHandshakeStart(eventData.TimeStamp, isServer, targetHost);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "HandshakeStop" && payload.Count == 1);
                {
                    var protocol = (SslProtocols)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnHandshakeStop(eventData.TimeStamp, protocol);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "HandshakeFailed" && payload.Count == 3);
                {
                    var isServer = (bool)payload[0];
                    var elapsed = TimeSpan.FromMilliseconds((double)payload[1]);
                    var exceptionMessage = (string)payload[2];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnHandshakeFailed(eventData.TimeStamp, isServer, elapsed, exceptionMessage);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(NetSecurityMetrics metrics, string name, double value)
    {
        switch (name)
        {
            case "tls-handshake-rate":
                metrics.TlsHandshakeRate = (long)value;
                break;

            case "total-tls-handshakes":
                metrics.TotalTlsHandshakes = (long)value;
                break;

            case "current-tls-handshakes":
                metrics.CurrentTlsHandshakes = (long)value;
                break;

            case "failed-tls-handshakes":
                metrics.FailedTlsHandshakes = (long)value;
                break;

            case "all-tls-sessions-open":
                metrics.TlsSessionsOpen = (long)value;
                break;

            case "tls10-sessions-open":
                metrics.Tls10SessionsOpen = (long)value;
                break;

            case "tls11-sessions-open":
                metrics.Tls11SessionsOpen = (long)value;
                break;

            case "tls12-sessions-open":
                metrics.Tls12SessionsOpen = (long)value;
                break;

            case "tls13-sessions-open":
                metrics.Tls13SessionsOpen = (long)value;
                break;

            case "all-tls-handshake-duration":
                metrics.TlsHandshakeDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "tls10-handshake-duration":
                metrics.Tls10HandshakeDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "tls11-handshake-duration":
                metrics.Tls11HandshakeDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "tls12-handshake-duration":
                metrics.Tls12HandshakeDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "tls13-handshake-duration":
                metrics.Tls13HandshakeDuration = TimeSpan.FromMilliseconds(value);
                break;

            default:
                return false;
        }

        return true;
    }
}
