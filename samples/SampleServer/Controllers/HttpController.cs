// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SampleServer.Controllers
{
    /// <summary>
    /// Sample controller.
    /// </summary>
    [ApiController]
    public class HttpController : ControllerBase
    {
        /// <summary>
        /// Returns a 200 response.
        /// </summary>
        [HttpGet]
        [Route("/api/noop")]
        public void NoOp()
        {
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet, HttpPost]
        [Route("/api/dump")]
        [Route("/{**catchall}", Order = int.MaxValue)] // Make this the default route if nothing matches
        public async Task<IActionResult> Dump()
        {
            var result = new {
                Request.Protocol,
                Request.Method,
                Request.Scheme,
                Host = Request.Host.Value,
                PathBase = Request.PathBase.Value,
                Path = Request.Path.Value,
                Query = Request.QueryString.Value,
                Headers = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
                Time = DateTimeOffset.UtcNow,
                Body = await new StreamReader(Request.Body).ReadToEndAsync(),
            };

            return Ok(result);
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/statuscode")]
        public void Status(int statusCode)
        {
            Response.StatusCode = statusCode;
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/headers")]
        public void Headers([FromBody] Dictionary<string, string> headers)
        {
            foreach (var (key, value) in headers)
            {
                Response.Headers[key] = value;
            }
        }
    }
}
