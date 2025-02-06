// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Utilities;

/// <inheritdoc/>
internal sealed class RandomFactory : IRandomFactory
{
    /// <inheritdoc/>
    public Random CreateRandomInstance()
    {
        return Random.Shared;
    }
}
