// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Yarp.ReverseProxy.Model;

public class ReverseProxyApplicationBuilder : IReverseProxyApplicationBuilder
{
    private readonly IApplicationBuilder _applicationBuilder;

    public ReverseProxyApplicationBuilder(IApplicationBuilder applicationBuilder)
    {
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        _applicationBuilder = applicationBuilder;
    }

    public IServiceProvider ApplicationServices
    {
        get => _applicationBuilder.ApplicationServices;
        set => _applicationBuilder.ApplicationServices = value;
    }

    public IFeatureCollection ServerFeatures => _applicationBuilder.ServerFeatures;

    public IDictionary<string, object?> Properties => _applicationBuilder.Properties;

    public RequestDelegate Build() => _applicationBuilder.Build();

    public IApplicationBuilder New() => _applicationBuilder.New();

    public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        => _applicationBuilder.Use(middleware);
}
