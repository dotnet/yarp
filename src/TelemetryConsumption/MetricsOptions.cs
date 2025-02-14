// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

internal static class MetricsOptions
{
    // TODO: Should this be publicly configurable? It's currently only visible to tests to reduce execution time
    public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
}
