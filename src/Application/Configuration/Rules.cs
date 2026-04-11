// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class HeaderRule
{
    public string Match { get; set; } = "";
    public Dictionary<string, string> Set { get; set; } = new();
}

public sealed class RedirectRule
{
    public string Match { get; set; } = "";
    public string Destination { get; set; } = "";
    public int StatusCode { get; set; } = 301;
}
