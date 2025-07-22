// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal sealed class HttpUpgradeFeatureWrapper : IHttpUpgradeFeature
{
    private readonly TimeProvider _timeProvider;

    public HttpContext HttpContext { get; private set; }

    public IHttpUpgradeFeature InnerUpgradeFeature { get; private set; }

    public WebSocketsTelemetryStream? TelemetryStream { get; private set; }

    public bool IsUpgradableRequest => InnerUpgradeFeature.IsUpgradableRequest;

    public HttpUpgradeFeatureWrapper(TimeProvider timeProvider, HttpContext httpContext, IHttpUpgradeFeature upgradeFeature)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(upgradeFeature);

        _timeProvider = timeProvider;
        HttpContext = httpContext;
        InnerUpgradeFeature = upgradeFeature;
    }

    public async Task<Stream> UpgradeAsync()
    {
        Debug.Assert(TelemetryStream is null);
        var opaqueTransport = await InnerUpgradeFeature.UpgradeAsync();

        if (HttpContext.Response.Headers.TryGetValue(HeaderNames.Upgrade, out var upgradeValues) &&
            upgradeValues.Count == 1 &&
            string.Equals("WebSocket", upgradeValues.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TelemetryStream = new WebSocketsTelemetryStream(_timeProvider, opaqueTransport);
        }

        return TelemetryStream ?? opaqueTransport;
    }
}
