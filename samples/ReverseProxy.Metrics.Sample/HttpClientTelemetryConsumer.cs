// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Yarp.Telemetry.Consumption;

namespace Yarp.Sample
{
    public sealed class HttpClientTelemetryConsumer : IHttpTelemetryConsumer
    {
        public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestStartOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestStopOffset = metrics.CalcOffset(timestamp);
        }

        public void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpConnectionEstablishedOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestLeftQueueOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestHeadersStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestHeadersStartOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestHeadersStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestHeadersStopOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestContentStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestContentStartOffset = metrics.CalcOffset(timestamp);
        }

        public void OnRequestContentStop(DateTime timestamp, long contentLength)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpRequestContentStopOffset = metrics.CalcOffset(timestamp);
        }

        public void OnResponseHeadersStart(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpResponseHeadersStartOffset = metrics.CalcOffset(timestamp);
        }

        public void OnResponseHeadersStop(DateTime timestamp)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.HttpResponseHeadersStopOffset = metrics.CalcOffset(timestamp);
        }
    }
}
