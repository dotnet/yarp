// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class HttpEventListenerService : EventListenerService<HttpEventListenerService, IHttpTelemetryConsumer, HttpMetrics>
{
    protected override string EventSourceName => "System.Net.Http";

    protected override int NumberOfMetrics => 11;

    public HttpEventListenerService(ILogger<HttpEventListenerService> logger, IEnumerable<IHttpTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<HttpMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(IHttpTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 7);
                {
                    var scheme = (string)payload[0];
                    var host = (string)payload[1];
                    var port = (int)payload[2];
                    var pathAndQuery = (string)payload[3];
                    var versionMajor = (int)(byte)payload[4];
                    var versionMinor = (int)(byte)payload[5];
                    var versionPolicy = (HttpVersionPolicy)payload[6];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStart(eventData.TimeStamp, scheme, host, port, pathAndQuery, versionMajor, versionMinor, versionPolicy);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == (eventData.Version == 0 ? 0 : 1));
                {
                    var statusCode = (int)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStop(eventData.TimeStamp, statusCode);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "RequestFailed" && payload.Count == (eventData.Version == 0 ? 0 : 1));
                {
                    var exceptionMessage = (string)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestFailed(eventData.TimeStamp, exceptionMessage);
                    }
                }
                break;

            case 4:
                Debug.Assert(eventData.EventName == "ConnectionEstablished" && payload.Count == (eventData.Version == 0 ? 2 : 7));
                {
                    var versionMajor = (int)(byte)payload[0];
                    var versionMinor = (int)(byte)payload[1];
                    var connectionId = (long)payload[2];
                    var scheme = (string)payload[3];
                    var host = (string)payload[4];
                    var port = (int)payload[5];
                    var remoteAddress = (string?)payload[6];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectionEstablished(eventData.TimeStamp, versionMajor, versionMinor, connectionId, scheme, host, port, remoteAddress);
                    }
                }
                break;

            case 5:
                Debug.Assert(eventData.EventName == "ConnectionClosed" && payload.Count == (eventData.Version == 0 ? 2 : 3));
                {
                    var versionMajor = (int)(byte)payload[0];
                    var versionMinor = (int)(byte)payload[1];
                    var connectionId = (long)payload[2];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectionClosed(eventData.TimeStamp, versionMajor, versionMinor, connectionId);
                    }
                }
                break;

            case 6:
                Debug.Assert(eventData.EventName == "RequestLeftQueue" && payload.Count == 3);
                {
                    var timeOnQueue = TimeSpan.FromMilliseconds((double)payload[0]);
                    var versionMajor = (int)(byte)payload[1];
                    var versionMinor = (int)(byte)payload[2];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestLeftQueue(eventData.TimeStamp, timeOnQueue, versionMajor, versionMinor);
                    }
                }
                break;

            case 7:
                Debug.Assert(eventData.EventName == "RequestHeadersStart" && payload.Count == (eventData.Version == 0 ? 0 : 1));
                {
                    var connectionId = (long)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestHeadersStart(eventData.TimeStamp, connectionId);
                    }
                }
                break;

            case 8:
                Debug.Assert(eventData.EventName == "RequestHeadersStop" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestHeadersStop(eventData.TimeStamp);
                    }
                }
                break;

            case 9:
                Debug.Assert(eventData.EventName == "RequestContentStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestContentStart(eventData.TimeStamp);
                    }
                }
                break;

            case 10:
                Debug.Assert(eventData.EventName == "RequestContentStop" && payload.Count == 1);
                {
                    var contentLength = (long)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestContentStop(eventData.TimeStamp, contentLength);
                    }
                }
                break;

            case 11:
                Debug.Assert(eventData.EventName == "ResponseHeadersStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseHeadersStart(eventData.TimeStamp);
                    }
                }
                break;

            case 12:
                Debug.Assert(eventData.EventName == "ResponseHeadersStop" && payload.Count == (eventData.Version == 0 ? 0 : 1));
                {
                    var statusCode = (int)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseHeadersStop(eventData.TimeStamp, statusCode);
                    }
                }
                break;

            case 13:
                Debug.Assert(eventData.EventName == "ResponseContentStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseContentStart(eventData.TimeStamp);
                    }
                }
                break;

            case 14:
                Debug.Assert(eventData.EventName == "ResponseContentStop" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseContentStop(eventData.TimeStamp);
                    }
                }
                break;

            case 15:
                Debug.Assert(eventData.EventName == "RequestFailedDetailed" && payload.Count == 1);
                // This event is more expensive to collect and requires an opt-in keyword.
                // We should only see it if a different EventListener opted in (potentially from a different process).
                break;

            case 16:
                Debug.Assert(eventData.EventName == "Redirect" && payload.Count == 1);
                {
                    var redirectUri = (string)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRedirect(eventData.TimeStamp, redirectUri);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(HttpMetrics metrics, string name, double value)
    {
        switch (name)
        {
            case "requests-started":
                metrics.RequestsStarted = (long)value;
                break;

            case "requests-started-rate":
                metrics.RequestsStartedRate = (long)value;
                break;

            case "requests-failed":
                metrics.RequestsFailed = (long)value;
                break;

            case "requests-failed-rate":
                metrics.RequestsFailedRate = (long)value;
                break;

            case "current-requests":
                metrics.CurrentRequests = (long)value;
                break;

            case "http11-connections-current-total":
                metrics.CurrentHttp11Connections = (long)value;
                break;

            case "http20-connections-current-total":
                metrics.CurrentHttp20Connections = (long)value;
                break;

            case "http11-requests-queue-duration":
                metrics.Http11RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "http20-requests-queue-duration":
                metrics.Http20RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "http30-connections-current-total":
                metrics.CurrentHttp30Connections = (long)value;
                break;

            case "http30-requests-queue-duration":
                metrics.Http30RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                break;

            default:
                return false;
        }

        return true;
    }
}
