// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Stores errors and exceptions that occurred when forwarding the request to the destination.
/// </summary>
public interface IForwarderErrorFeature
{
    /// <summary>
    /// The specified ProxyError.
    /// </summary>
    ForwarderError Error { get; }

    /// <summary>
    /// An Exception that occurred when forwarding the request to the destination, if any.
    /// </summary>
    Exception? Exception { get; }
}
