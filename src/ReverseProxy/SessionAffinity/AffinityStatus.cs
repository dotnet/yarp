// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Affinity resolution status.
/// </summary>
public enum AffinityStatus
{
    OK,
    AffinityKeyNotSet,
    AffinityKeyExtractionFailed,
    DestinationNotFound
}
