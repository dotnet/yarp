// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Health;

/// <summary>
/// Stores a new active health state for the given destination.
/// </summary>
public readonly struct NewActiveDestinationHealth
{
    public NewActiveDestinationHealth(DestinationState destination, DestinationHealth newActiveHealth)
    {
        Destination = destination;
        NewActiveHealth = newActiveHealth;
    }

    public DestinationState Destination { get; }

    public DestinationHealth NewActiveHealth { get; }
}
