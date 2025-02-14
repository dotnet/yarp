// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.Telemetry.Consumption;

namespace Yarp.Sample
{
    public sealed class ForwarderMetricsConsumer : IMetricsConsumer<ForwarderMetrics>
    {
        public void OnMetrics(ForwarderMetrics previous, ForwarderMetrics current)
        {
            var elapsed = current.Timestamp - previous.Timestamp;
            var newRequests = current.RequestsStarted - previous.RequestsStarted;
            Console.Title = $"Forwarded {current.RequestsStarted} requests ({newRequests} in the last {(int)elapsed.TotalMilliseconds} ms)";
        }
    }
}
