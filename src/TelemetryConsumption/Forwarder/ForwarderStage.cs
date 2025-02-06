// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Telemetry.Consumption;

/// <summary>
/// Stages of forwarding a request.
/// </summary>
public enum ForwarderStage : int
{
    SendAsyncStart = 1,
    SendAsyncStop,
    RequestContentTransferStart,
    ResponseContentTransferStart,
    ResponseUpgrade,
}
