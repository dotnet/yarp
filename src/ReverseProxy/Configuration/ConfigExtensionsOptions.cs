// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration;

public class ConfigExtensionsOptions
{
    public Dictionary<string, Type> RouteExtensions { get; } = new();
    public Dictionary<string, Type> ClusterExtensions { get; } = new();
}
