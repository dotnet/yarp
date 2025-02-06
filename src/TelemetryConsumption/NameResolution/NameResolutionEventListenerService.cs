// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class NameResolutionEventListenerService : EventListenerService<NameResolutionEventListenerService, INameResolutionTelemetryConsumer, NameResolutionMetrics>
{
    protected override string EventSourceName => "System.Net.NameResolution";

    protected override int NumberOfMetrics => 3;

    public NameResolutionEventListenerService(ILogger<NameResolutionEventListenerService> logger, IEnumerable<INameResolutionTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<NameResolutionMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(INameResolutionTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "ResolutionStart" && payload.Count == 1);
                {
                    var hostNameOrAddress = (string)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResolutionStart(eventData.TimeStamp, hostNameOrAddress);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "ResolutionStop" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResolutionStop(eventData.TimeStamp);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "ResolutionFailed" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResolutionFailed(eventData.TimeStamp);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(NameResolutionMetrics metrics, string name, double value)
    {
        switch (name)
        {
            case "dns-lookups-requested":
                metrics.DnsLookupsRequested = (long)value;
                break;

            case "dns-lookups-duration":
                metrics.AverageLookupDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "current-dns-lookups":
                metrics.CurrentDnsLookups = (long)value;
                break;

            default:
                return false;
        }

        return true;
    }
}
