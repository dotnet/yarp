// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;

namespace Yarp.ReverseProxy.Sample.Controllers;

/// <summary>
/// Controller for active health check probes.
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    private static volatile int _count;
    /// <summary>
    /// Returns 200 if server is healthy.
    /// </summary>
    [HttpGet]
    [Route("/api/health")]
    public IActionResult CheckHealth()
    {
        _count++;
        // Simulate temporary health degradation.
        return _count % 10 < 4 ? Ok() : StatusCode(500);
    }
}
