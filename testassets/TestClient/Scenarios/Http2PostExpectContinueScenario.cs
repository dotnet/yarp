// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios;

/// <summary>
/// Verifies that YARP correctly handles the case where the client specifies
/// <c>Expect: 100-continue</c> and the destination fails early without accepting the request body.
/// This scenario can be encountered in real world scenarios, usually when authentication fails on the destination.
/// The <c>Expect: 100-continue</c> behavior causes the request body copy to not even start on YARP in this case.
/// </summary>
internal sealed class Http2PostExpectContinueScenario : IScenario
{
    public async Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation)
    {
        using (var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
        })
        using (var client = new HttpMessageInvoker(handler))
        {
            var targetUri = new Uri(new Uri(args.Target, UriKind.Absolute), "api/skipbody");
            var stopwatch = Stopwatch.StartNew();
            var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            request.Version = new Version(2, 0);
            request.Headers.ExpectContinue = true;
            request.Content = new StringContent(new string('a', 1024 * 1024 * 10));
            Console.WriteLine($"Calling {targetUri} with HTTP/2");

            var response = await client.SendAsync(request, cancellation);
            Console.WriteLine($"Received response: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds} ms");
            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"Expected status 409 Conflict!");
            }
        }
    }
}
