// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Forwarder;

internal sealed class ForwarderErrorFeature : IForwarderErrorFeature
{
    internal ForwarderErrorFeature(ForwarderError error, Exception? ex)
    {
        Error = error;
        Exception = ex;
    }

    /// <summary>
    /// The specified ForwarderError.
    /// </summary>
    public ForwarderError Error { get; }

    /// <summary>
    /// The error, if any.
    /// </summary>
    public Exception? Exception { get; }
}
