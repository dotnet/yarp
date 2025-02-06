// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy.Utilities;

/// <summary>
/// Factory for creating random class. This factory let us able to inject random class into other class.
/// So that we can mock the random class for unit test.
/// </summary>
public interface IRandomFactory
{
    /// <summary>
    /// Create a instance of random class.
    /// </summary>
    Random CreateRandomInstance();
}
