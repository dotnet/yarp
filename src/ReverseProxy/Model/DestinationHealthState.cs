// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Tracks destination passive and active health states.
/// </summary>
public class DestinationHealthState
{
    private volatile DestinationHealth _active;
    private volatile DestinationHealth _passive;

    /// <summary>
    /// Passive health state.
    /// </summary>
    public DestinationHealth Passive
    {
        get => _passive;
        set => _passive = value;
    }

    /// <summary>
    /// Active health state.
    /// </summary>
    public DestinationHealth Active
    {
        get => _active;
        set => _active = value;
    }
}
