// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class StaticFilesOptions
{
    public bool Enabled { get; set; }
    public bool CleanUrls { get; set; }
    public string? TrailingSlash { get; set; }
    public bool PreCompressed { get; set; }
    public Dictionary<int, string>? ErrorPages { get; set; }
}
