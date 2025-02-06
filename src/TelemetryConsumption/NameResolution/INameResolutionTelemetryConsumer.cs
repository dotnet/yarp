// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// A consumer of System.Net.NameResolution EventSource events.
/// </summary>
public interface INameResolutionTelemetryConsumer
{
    /// <summary>
    /// Called before a name resolution.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    /// <param name="hostNameOrAddress">Host name or address we are resolving.</param>
    void OnResolutionStart(DateTime timestamp, string hostNameOrAddress) { }

    /// <summary>
    /// Called after a name resolution.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResolutionStop(DateTime timestamp) { }

    /// <summary>
    /// Called before <see cref="OnResolutionStop(DateTime)"/> if the name resolution failed.
    /// </summary>
    /// <param name="timestamp">Timestamp when the event was fired.</param>
    void OnResolutionFailed(DateTime timestamp) { }
}
