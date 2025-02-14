// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// For use with <see cref="RequestHeaderForwardedTransform"/>.
/// </summary>
public enum NodeFormat
{
    None,
    Random,
    RandomAndPort,
    RandomAndRandomPort,
    Unknown,
    UnknownAndPort,
    UnknownAndRandomPort,
    Ip,
    IpAndPort,
    IpAndRandomPort,
}
