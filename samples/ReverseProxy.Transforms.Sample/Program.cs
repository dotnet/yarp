// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms;
using Yarp.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<MyTransformProvider>() // Adds custom transforms via code.
    .AddTransformFactory<MyTransformFactory>() // Adds custom transforms via config.
    .AddTransforms(transformBuilderContext =>  // Add transforms inline
    {
        // For each route+cluster pair decide if we want to add transforms, and if so, which?
        // This logic is re-run each time a route is rebuilt.

        transformBuilderContext.AddPathPrefix("/prefix");

        // Only do this for routes that require auth.
        if (string.Equals("token", transformBuilderContext.Route.AuthorizationPolicy))
        {
            transformBuilderContext.AddRequestTransform(async transformContext =>
            {
                // AuthN and AuthZ will have already been completed after request routing.
                var ticket = await transformContext.HttpContext.AuthenticateAsync("token");
                var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<TokenService>();
                var token = await tokenService.GetAuthTokenAsync(ticket.Principal);
                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            });
        }
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Register the reverse proxy routes
app.MapReverseProxy();

app.Run();
