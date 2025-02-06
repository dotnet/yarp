// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Health;

namespace Yarp.ReverseProxy.Sample.Controllers;

/// <summary>
/// Controller for health check api.
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    private readonly IActiveHealthCheckMonitor _healthCheckMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthController" /> class.
    /// </summary>
    public HealthController(IActiveHealthCheckMonitor healthCheckMonitor)
    {
        _healthCheckMonitor = healthCheckMonitor;
    }

    /// <summary>
    /// Returns 200 if Proxy is healthy.
    /// </summary>
    [HttpGet]
    [Route("/api/health")]
    public IActionResult CheckHealth()
    {
        // TODO: Implement health controller, use guid in route.
        return _healthCheckMonitor.InitialProbeCompleted ? Ok() : StatusCode(503);
    }
}
