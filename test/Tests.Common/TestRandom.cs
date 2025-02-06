// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.Tests.Common;

public class TestRandom : Random
{
    public int[] Sequence { get; set; }
    public int Offset { get; set; }

    public override int Next(int maxValue)
    {
        return Sequence[Offset++];
    }
}
