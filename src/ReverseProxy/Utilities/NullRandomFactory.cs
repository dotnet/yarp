// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class NullRandomFactory : IRandomFactory
{
    public Random CreateRandomInstance()
    {
        throw new NotImplementedException();
    }
}
