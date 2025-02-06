// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Connections;
using Yarp.ReverseProxy.Sample;
using Yarp.ReverseProxy.Forwarder;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;
using System.Net.Http;
using System.Threading;
using System;
using System.Net;
using System.Diagnostics;
using Yarp.ReverseProxy.Transforms.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    var logger = kestrel.ApplicationServices.GetRequiredService<ILogger<Program>>();
    kestrel.ListenAnyIP(5001, portOptions =>
    {
        portOptions.Use(async (connectionContext, next) =>
        {
            await TlsFilter.ProcessAsync(connectionContext, next, logger);
        });
        portOptions.UseHttps();
    });
});

builder.Services.AddHttpForwarder();

var app = builder.Build();

var httpClient = new HttpMessageInvoker(new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    EnableMultipleHttp2Connections = true,
    ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
    ConnectTimeout = TimeSpan.FromSeconds(15),
});

var transformBuilder = app.Services.GetRequiredService<ITransformBuilder>();
var transformer = transformBuilder.Create(context =>
{
    context.AddQueryRemoveKey("param1");
    context.AddQueryValue("area", "xx2", false);
    context.AddOriginalHost(false);
});

// or var transformer = new CustomTransformer();
// or var transformer = HttpTransformer.Default;

var requestConfig = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) };

app.MapForwarder("/{**catch-all}", "https://example.com", requestConfig, transformer, httpClient);

app.Run();

internal sealed class CustomTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
    {
        // Copy all request headers
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        // Customize the query string:
        var queryContext = new QueryTransformContext(httpContext.Request);
        queryContext.Collection.Remove("param1");
        queryContext.Collection["area"] = "xx2";

        // Assign the custom uri. Be careful about extra slashes when concatenating here.
        proxyRequest.RequestUri = new Uri(destinationPrefix + httpContext.Request.Path + queryContext.QueryString);

        // Suppress the original request header, use the one from the destination Uri.
        proxyRequest.Headers.Host = null;
    }

    public override ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
    {
        // Suppress the response body from errors.
        // The status code was already copied.
        if (!proxyResponse.IsSuccessStatusCode)
        {
            return new ValueTask<bool>(false);
        }

        return base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
    }
}
