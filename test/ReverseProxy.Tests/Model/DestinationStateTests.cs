// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Yarp.ReverseProxy.Model.Tests;

public class DestinationStateTests
{
    [Fact]
    public void DestinationInfoEnumerator()
    {
        var destinationInfo = new DestinationState("destination1");
        var list = new List<DestinationState>();

        foreach (var item in destinationInfo)
        {
            list.Add(item);
        }

        var first = Assert.Single(list);
        Assert.Same(destinationInfo, first);
    }

    [Fact]
    public void DestinationInfoReadOnlyList()
    {
        var destinationInfo = new DestinationState("destination2");

        IReadOnlyList<DestinationState> list = destinationInfo;

        Assert.Single(list);
        Assert.Same(destinationInfo, list[0]);
        Assert.Throws<IndexOutOfRangeException>(() => list[1]);
    }
}
