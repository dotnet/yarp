// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

/// <summary>
/// Console output mode. Named LogMode to avoid confusion with Microsoft.Extensions.Logging.LogLevel.
/// </summary>
public enum LogMode
{
    /// <summary>Banner plus warnings/errors. Proxy forwarding failures are condensed to a single line.</summary>
    Default,

    /// <summary>Default behavior plus one line per completed request.</summary>
    Requests,

    /// <summary>Full framework logging, request lines and a resolved config dump at startup.</summary>
    Debug,
}

public sealed class LogOptions
{
    public LogMode Level { get; set; }
}
