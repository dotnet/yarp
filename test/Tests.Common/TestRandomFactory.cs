// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.Tests.Common;

public class TestRandomFactory : IRandomFactory
{
    public TestRandom Instance { get; set; }

    public Random CreateRandomInstance()
    {
        return Instance;
    }
}
