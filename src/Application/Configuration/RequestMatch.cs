// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class RequestMatch
{
    public string? Path { get; set; }

    public List<string> Hosts { get; set; } = [];

    public List<string> Methods { get; set; } = [];

    public List<Yarp.ReverseProxy.Configuration.RouteQueryParameter> QueryParameters { get; set; } = [];
}
