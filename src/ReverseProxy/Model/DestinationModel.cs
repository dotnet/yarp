// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Immutable representation of the portions of a destination
/// that only change in reaction to configuration changes
/// (e.g. address).
/// </summary>
/// <remarks>
/// All members must remain immutable to avoid thread safety issues.
/// Instead, instances of <see cref="DestinationModel"/> are replaced
/// in their entirety when values need to change.
/// </remarks>
public sealed class DestinationModel
{
    /// <summary>
    /// Creates a new instance. This constructor is for tests and infrastructure, this type is normally constructed by
    /// the configuration loading infrastructure.
    /// </summary>
    public DestinationModel(DestinationConfig destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Config = destination;
    }

    /// <summary>
    /// This destination's configuration.
    /// </summary>
    public DestinationConfig Config { get; }

    internal bool HasChanged(DestinationConfig destination)
    {
        return Config != destination;
    }
}
