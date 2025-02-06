// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Telemetry.Consumption;

internal sealed class ForwarderEventListenerService : EventListenerService<ForwarderEventListenerService, IForwarderTelemetryConsumer, ForwarderMetrics>
{
    protected override string EventSourceName => "Yarp.ReverseProxy";

    protected override int NumberOfMetrics => 4;

    public ForwarderEventListenerService(ILogger<ForwarderEventListenerService> logger, IEnumerable<IForwarderTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<ForwarderMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(IForwarderTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "ForwarderStart" && payload.Count == 1);
                {
                    var destinationPrefix = (string)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnForwarderStart(eventData.TimeStamp, destinationPrefix);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "ForwarderStop" && payload.Count == 1);
                {
                    var statusCode = (int)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnForwarderStop(eventData.TimeStamp, statusCode);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "ForwarderFailed" && payload.Count == 1);
                {
                    var error = (ForwarderError)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnForwarderFailed(eventData.TimeStamp, error);
                    }
                }
                break;

            case 4:
                Debug.Assert(eventData.EventName == "ForwarderStage" && payload.Count == 1);
                {
                    var proxyStage = (ForwarderStage)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnForwarderStage(eventData.TimeStamp, proxyStage);
                    }
                }
                break;

            case 5:
                Debug.Assert(eventData.EventName == "ContentTransferring" && payload.Count == 5);
                {
                    var isRequest = (bool)payload[0];
                    var contentLength = (long)payload[1];
                    var iops = (long)payload[2];
                    var readTime = new TimeSpan((long)payload[3]);
                    var writeTime = new TimeSpan((long)payload[4]);
                    foreach (var consumer in consumers)
                    {
                        consumer.OnContentTransferring(eventData.TimeStamp, isRequest, contentLength, iops, readTime, writeTime);
                    }
                }
                break;

            case 6:
                Debug.Assert(eventData.EventName == "ContentTransferred" && payload.Count == 6);
                {
                    var isRequest = (bool)payload[0];
                    var contentLength = (long)payload[1];
                    var iops = (long)payload[2];
                    var readTime = new TimeSpan((long)payload[3]);
                    var writeTime = new TimeSpan((long)payload[4]);
                    var firstReadTime = new TimeSpan((long)payload[5]);
                    foreach (var consumer in consumers)
                    {
                        consumer.OnContentTransferred(eventData.TimeStamp, isRequest, contentLength, iops, readTime, writeTime, firstReadTime);
                    }
                }
                break;

            case 7:
                Debug.Assert(eventData.EventName == "ForwarderInvoke" && payload.Count == 3);
                {
                    var clusterId = (string)payload[0];
                    var routeId = (string)payload[1];
                    var destinationId = (string)payload[2];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnForwarderInvoke(eventData.TimeStamp, clusterId, routeId, destinationId);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(ForwarderMetrics metrics, string name, double value)
    {
        var longValue = (long)value;

        switch (name)
        {
            case "requests-started":
                metrics.RequestsStarted = longValue;
                break;

            case "requests-started-rate":
                metrics.RequestsStartedRate = longValue;
                break;

            case "requests-failed":
                metrics.RequestsFailed = longValue;
                break;

            case "current-requests":
                metrics.CurrentRequests = longValue;
                break;

            default:
                return false;
        }

        return true;
    }
}
