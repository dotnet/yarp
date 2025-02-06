// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;

namespace Yarp.ReverseProxy.Sample.Controllers;

/// <summary>
/// Controller for health check api.
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns 200 if Proxy is healthy.
    /// </summary>
    [HttpGet]
    [Route("/api/health")]
    public IActionResult CheckHealth()
    {
        return Ok();
    }
}
