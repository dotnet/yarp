// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class RedirectRule
{
    public RequestMatch Match { get; set; } = new();

    public string? Destination { get; set; }

    public int StatusCode { get; set; } = 301;
}
