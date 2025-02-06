// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusForwarderMetrics : IMetricsConsumer<ForwarderMetrics>
    {
        private static readonly Counter _requestsStarted = Metrics.CreateCounter(
            "yarp_proxy_requests_started",
            "Number of requests initiated through the proxy"
            );

        private static readonly Counter _requestsFailed = Metrics.CreateCounter(
            "yarp_proxy_requests_failed",
            "Number of proxy requests that have failed"
            );

        private static readonly Gauge _CurrentRequests = Metrics.CreateGauge(
            "yarp_proxy_current_requests",
            "Number of active proxy requests that have started but not yet completed or failed"
            );

        public void OnMetrics(ForwarderMetrics previous, ForwarderMetrics current)
        {
            _requestsStarted.IncTo(current.RequestsStarted);
            _requestsFailed.IncTo(current.RequestsFailed);
            _CurrentRequests.Set(current.CurrentRequests);
        }
    }
}
