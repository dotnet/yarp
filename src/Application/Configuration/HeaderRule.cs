// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class HeaderRule
{
    public RequestMatch Match { get; set; } = new();

    public Dictionary<string, string?> Set { get; set; } = [];
}
