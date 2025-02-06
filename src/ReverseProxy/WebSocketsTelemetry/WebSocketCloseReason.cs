// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal enum WebSocketCloseReason : int
{
    Unknown,
    ClientGracefulClose,
    ServerGracefulClose,
    ClientDisconnect,
    ServerDisconnect,
    ActivityTimeout,
}
