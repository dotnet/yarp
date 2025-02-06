// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SampleServer.Controllers;

/// <summary>
/// Sample controller.
/// </summary>
[ApiController]
public class UpgradeController : ControllerBase
{
    private readonly ILogger<UpgradeController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpgradeController" /> class.
    /// </summary>
    public UpgradeController(ILogger<UpgradeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Upgrades the connection to a raw socket stream, then implements a simple byte ping/pong server.
    /// Note that this does not use WebSockets, and relies solely on HTTP/1.1 connection upgrade mechanism.
    /// </summary>
    [HttpGet]
    [Route("/api/rawupgrade")]
    public async Task RawUpgrade()
    {
        var upgradeFeature = HttpContext.Features.Get<IHttpUpgradeFeature>();
        if (upgradeFeature is null || !upgradeFeature.IsUpgradableRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }

        await using var stream = await upgradeFeature.UpgradeAsync();
        _logger.LogInformation("Upgraded connection.");
        await RunPingPongAsync(stream);
        _logger.LogInformation("Finished.");
    }

    /// <summary>
    /// Simple echo protocol that echo's each received byte.
    /// <c>255</c> is treated as a special "goodbye" message, which causes us to drop the connection.
    /// </summary>
    private async Task RunPingPongAsync(Stream stream)
    {
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer, HttpContext.RequestAborted) != 0)
        {
            if (buffer[0] == 255)
            {
                // Goodbye
                break;
            }

            await stream.WriteAsync(buffer);
        }
    }
}
