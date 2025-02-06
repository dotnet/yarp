// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusKestrelMetrics : IMetricsConsumer<KestrelMetrics>
    {
        private static readonly Counter _totalConnections = Metrics.CreateCounter(
            "yarp_kestrel_total_connections",
            "Number of incoming connections opened"
            );

        private static readonly Counter _totalTlsHandshakes = Metrics.CreateCounter(
            "yarp_kestrel_total_tls_Handshakes",
            "Number of TLS handshakes started"
            );

        private static readonly Gauge _currentTlsHandshakes = Metrics.CreateGauge(
            "yarp_kestrel_current_tls_handshakes",
            "Number of active TLS handshakes that have started but not yet completed or failed"
            );

        private static readonly Counter _failedTlsHandshakes = Metrics.CreateCounter(
            "yarp_kestrel_failed_tls_handshakes",
            "Number of TLS handshakes that failed"
            );

        private static readonly Gauge _currentConnections = Metrics.CreateGauge(
            "yarp_kestrel_current_connections",
            "Number of currently open incoming connections"
            );

        private static readonly Gauge _connectionQueueLength = Metrics.CreateGauge(
            "yarp_kestrel_connection_queue_length",
            "Number of connections on the queue."
            );

        private static readonly Gauge _requestQueueLength = Metrics.CreateGauge(
            "yarp_kestrel_request_queue_length",
            "Number of requests on the queue"
            );

        public void OnMetrics(KestrelMetrics previous, KestrelMetrics current)
        {
            _totalConnections.IncTo(current.TotalConnections);
            _totalTlsHandshakes.IncTo(current.TotalTlsHandshakes);
            _currentTlsHandshakes.Set(current.CurrentTlsHandshakes);
            _failedTlsHandshakes.IncTo(current.FailedTlsHandshakes);
            _currentConnections.Set(current.CurrentConnections);
            _connectionQueueLength.Set(current.ConnectionQueueLength);
            _requestQueueLength.Set(current.RequestQueueLength);
        }
    }
}
