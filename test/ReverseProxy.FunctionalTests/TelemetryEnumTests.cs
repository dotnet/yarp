// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Yarp.ReverseProxy;

public class TelemetryEnumTests
{
    [Theory]
    [InlineData(typeof(Telemetry.Consumption.ForwarderStage), typeof(Forwarder.ForwarderStage))]
    [InlineData(typeof(Telemetry.Consumption.WebSocketCloseReason), typeof(WebSocketsTelemetry.WebSocketCloseReason))]
    public void ExposedEnumsMatchInternalCopies(Type publicEnum, Type internalEnum)
    {
        Assert.Equal(internalEnum.GetEnumNames(), publicEnum.GetEnumNames());
        Assert.Equal(internalEnum.GetEnumValues().Cast<int>().ToArray(), publicEnum.GetEnumValues().Cast<int>().ToArray());
    }
}
