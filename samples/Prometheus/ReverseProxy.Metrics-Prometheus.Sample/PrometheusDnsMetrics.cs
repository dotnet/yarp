// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusDnsMetrics : IMetricsConsumer<NameResolutionMetrics>
    {
        private static readonly Counter _dnsLookupsRequested = Metrics.CreateCounter(
            "yarp_dns_lookups_requested",
            "Number of DNS lookups requested"
            );

        private static readonly Gauge _averageLookupDuration = Metrics.CreateGauge(
            "yarp_dns_average_lookup_duration",
            "Average DNS lookup duration"
            );

        public void OnMetrics(NameResolutionMetrics previous, NameResolutionMetrics current)
        {
            _dnsLookupsRequested.IncTo(current.DnsLookupsRequested);
            _averageLookupDuration.Set(current.AverageLookupDuration.TotalMilliseconds);
        }
    }
}
