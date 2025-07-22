// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Yarp.ReverseProxy.Forwarder;
using System.Text.Json;

namespace Yarp.Sample
{
    public class PerRequestMetrics
    {
        private static readonly AsyncLocal<PerRequestMetrics> _local = new AsyncLocal<PerRequestMetrics>();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        // Ensure we are only fetched via the factory
        private PerRequestMetrics() { }

        /// <summary>
        /// Factory to instantiate or restore the metrics from AsyncLocal storage
        /// </summary>
        public static PerRequestMetrics Current => _local.Value ??= new PerRequestMetrics();

        // Time the request was started via the pipeline
        public DateTime StartTime { get; set; }

        // Offset Tics for each part of the proxy operation
        public float RouteInvokeOffset { get; set; }
        public float ProxyStartOffset { get; set; }
        public float HttpRequestStartOffset { get; set; }
        public float HttpConnectionEstablishedOffset { get; set; }
        public float HttpRequestLeftQueueOffset { get; set; }

        public float HttpRequestHeadersStartOffset { get; set; }
        public float HttpRequestHeadersStopOffset { get; set; }
        public float HttpRequestContentStartOffset { get; set; }
        public float HttpRequestContentStopOffset { get; set; }

        public float HttpResponseHeadersStartOffset { get; set; }
        public float HttpResponseHeadersStopOffset { get; set; }
        public float HttpResponseContentStopOffset { get; set; }

        public float HttpRequestStopOffset { get; set; }
        public float ProxyStopOffset { get; set; }

        // Info about the request
        public ForwarderError Error { get; set; }
        public long RequestBodyLength { get; set; }
        public long ResponseBodyLength { get; set; }
        public long RequestContentIops { get; set; }
        public long ResponseContentIops { get; set; }
        public string DestinationId { get; set; }
        public string ClusterId { get; set; }
        public string RouteId { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, _jsonOptions);
        }

        public float CalcOffset(DateTime timestamp)
        {
            return (float)(timestamp - StartTime).TotalMilliseconds;
        }
    }
}

