// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of <typeparamref name="TMetrics"/>.
/// </summary>
public interface IMetricsConsumer<TMetrics>
{
    /// <summary>
    /// Processes <typeparamref name="TMetrics"/> from the last event counter interval.
    /// </summary>
    /// <param name="previous"><typeparamref name="TMetrics"/> collected in the previous interval.</param>
    /// <param name="current"><typeparamref name="TMetrics"/> collected in the last interval.</param>
    void OnMetrics(TMetrics previous, TMetrics current);
}
