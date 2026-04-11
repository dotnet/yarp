// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

/// <summary>
/// Root configuration for the YARP container application.
/// Bound from IConfiguration at startup — all application code uses this object model.
/// </summary>
public sealed class YarpAppConfig
{
    public StaticFilesOptions StaticFiles { get; set; } = new();
    public NavigationFallbackOptions NavigationFallback { get; set; } = new();
    public CompressionOptions Compression { get; set; } = new();
    public HttpsOptions Https { get; set; } = new();
    public TelemetryOptions Telemetry { get; set; } = new();
    public List<HeaderRule>? Headers { get; set; }
    public List<RedirectRule>? Redirects { get; set; }
}
