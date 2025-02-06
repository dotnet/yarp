// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios;

internal sealed class SessionAffinityScenario : IScenario
{
    public async Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            // Session affinity key will be stored in a cookie
            UseCookies = true,
            UseProxy = false
        };
        using var client = new HttpMessageInvoker(handler);
        var targetUri = new Uri(new Uri(args.Target, UriKind.Absolute), "api/dump");
        var stopwatch = Stopwatch.StartNew();

        var request0 = new HttpRequestMessage(HttpMethod.Get, targetUri) { Version = new Version(1, 1) };
        Console.WriteLine($"Sending first request to {targetUri} with HTTP/1.1");
        var response0 = await client.SendAsync(request0, cancellation);

        PrintDuration(stopwatch, response0);
        PrintAffinityCookie(handler, targetUri, response0);
        await ReadAndPrintBody(response0, cancellation);

        stopwatch.Reset();

        var request1 = new HttpRequestMessage(HttpMethod.Get, targetUri) { Version = new Version(1, 1) };
        Console.WriteLine($"Sending second request to {targetUri} with HTTP/1.1");
        var response1 = await client.SendAsync(request1, cancellation);

        PrintDuration(stopwatch, response1);
        PrintAffinityCookie(handler, targetUri, response1);
        await ReadAndPrintBody(response1, cancellation);
    }

    private static void PrintDuration(Stopwatch stopwatch, HttpResponseMessage response)
    {
        Console.WriteLine($"Received response: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds} ms");
        response.EnsureSuccessStatusCode();
    }

    private static void PrintAffinityCookie(HttpClientHandler handler, Uri targetUri, HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValue))
        {
            Console.WriteLine($"Received header Set-Cookie: {setCookieValue.ToArray()[0]}");
        }
        else
        {
            Console.WriteLine($"Response doesn't have Set-Cookie header.");
        }

        var affinityCookie = handler.CookieContainer.GetCookies(targetUri)[".Yarp.Affinity"];
        Console.WriteLine($"Affinity key stored on a cookie {affinityCookie.Value}");
    }

    private static async Task ReadAndPrintBody(HttpResponseMessage response, CancellationToken cancellation)
    {
        var body = await response.Content.ReadAsStringAsync(cancellation);
        var json = JsonDocument.Parse(body);
        Console.WriteLine(
            "Received response:" +
            $"{Environment.NewLine}" +
            $"{JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true })}");
        response.EnsureSuccessStatusCode();
    }
}
