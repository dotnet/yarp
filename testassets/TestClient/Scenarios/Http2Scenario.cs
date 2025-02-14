// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios;

internal sealed class Http2Scenario : IScenario
{
    public async Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false
        };
        using var client = new HttpMessageInvoker(handler);
        var targetUri = new Uri(new Uri(args.Target, UriKind.Absolute), "api/dump");
        var stopwatch = Stopwatch.StartNew();
        var request = new HttpRequestMessage(HttpMethod.Get, targetUri) { Version = new Version(2, 0) };
        Console.WriteLine($"Calling {targetUri} with HTTP/2");

        var response = await client.SendAsync(request, cancellation);
        Console.WriteLine($"Received response: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds} ms");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellation);
        var json = JsonDocument.Parse(body);
        Console.WriteLine(
            $"Received response:" +
            $"{Environment.NewLine}" +
            $"{JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true })}");

        response.EnsureSuccessStatusCode();
    }
}
