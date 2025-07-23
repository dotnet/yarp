// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal sealed class HttpConnectFeatureWrapper : IHttpExtendedConnectFeature
{
    private readonly TimeProvider _timeProvider;

    public HttpContext HttpContext { get; private set; }

    public IHttpExtendedConnectFeature InnerConnectFeature { get; private set; }

    public WebSocketsTelemetryStream? TelemetryStream { get; private set; }

    public bool IsExtendedConnect => InnerConnectFeature.IsExtendedConnect;

    public string? Protocol => InnerConnectFeature.Protocol;

    public HttpConnectFeatureWrapper(TimeProvider timeProvider, HttpContext httpContext, IHttpExtendedConnectFeature connectFeature)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(connectFeature);

        _timeProvider = timeProvider;
        HttpContext = httpContext;
        InnerConnectFeature = connectFeature;
    }

    public async ValueTask<Stream> AcceptAsync()
    {
        Debug.Assert(TelemetryStream is null);
        var opaqueTransport = await InnerConnectFeature.AcceptAsync();
        TelemetryStream = new WebSocketsTelemetryStream(_timeProvider, opaqueTransport);
        return TelemetryStream;
    }
}
