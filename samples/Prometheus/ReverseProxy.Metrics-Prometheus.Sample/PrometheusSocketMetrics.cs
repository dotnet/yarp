// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.Telemetry.Consumption;
using Prometheus;

namespace Yarp.Sample
{
    public sealed class PrometheusSocketMetrics : IMetricsConsumer<SocketsMetrics>
    {
        private static readonly Counter _outgoingConnectionsEstablished = Metrics.CreateCounter(
            "yarp_sockets_outgoing_connections_established",
            "Number of outgoing (Connect) Socket connections established"
            );

        private static readonly Counter _incomingConnectionsEstablished = Metrics.CreateCounter(
            "yarp_sockets_incoming_connections_established",
            "Number of incoming (Accept) Socket connections established"
            );

        private static readonly Counter _bytesReceived = Metrics.CreateCounter(
            "yarp_sockets_bytes_received",
            "Number of bytes received"
            );

        private static readonly Counter _bytesSent = Metrics.CreateCounter(
            "yarp_sockets_bytes_sent",
            "Number of bytes sent"
            );

        private static readonly Counter _datagramsReceived = Metrics.CreateCounter(
            "yarp_sockets_datagrams_received",
            "Number of datagrams received"
            );

        private static readonly Counter _datagramsSent = Metrics.CreateCounter(
            "yarp_sockets_datagrams_sent",
            "Number of datagrams Sent"
            );

        public void OnMetrics(SocketsMetrics previous, SocketsMetrics current)
        {
            _outgoingConnectionsEstablished.IncTo(current.OutgoingConnectionsEstablished);
            _incomingConnectionsEstablished.IncTo(current.IncomingConnectionsEstablished);
            _bytesReceived.IncTo(current.BytesReceived);
            _bytesSent.IncTo(current.BytesSent);
            _datagramsReceived.IncTo(current.DatagramsReceived);
            _datagramsSent.IncTo(current.DatagramsSent);
        }
    }
}
