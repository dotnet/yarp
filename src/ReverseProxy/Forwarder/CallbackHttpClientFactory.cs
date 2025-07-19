// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Forwarder;

internal sealed class CallbackHttpClientFactory : ForwarderHttpClientFactory
{
    private readonly Action<ForwarderHttpClientContext, SocketsHttpHandler> _configureClient;

    internal CallbackHttpClientFactory(ILogger<ForwarderHttpClientFactory> logger,
        Action<ForwarderHttpClientContext, SocketsHttpHandler> configureClient) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(configureClient);
        _configureClient = configureClient;
    }

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);
        _configureClient(context, handler);
    }
}
