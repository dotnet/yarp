// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Forwarder;

public class ReverseProxyServiceCollectionTests
{

    [Fact]
    public void ConfigureHttpClient_Works()
    {
        new ServiceCollection()
            .AddReverseProxy()
            .ConfigureHttpClient((_, _) => { });
    }

    [Fact]
    public void ConfigureHttpClient_ThrowIfCustomServiceAdded()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            new ServiceCollection()
                .AddSingleton<IForwarderHttpClientFactory, CustomForwarderHttpClientFactory>()
                .AddReverseProxy()
                .ConfigureHttpClient((_, _) => { });
        });
    }

    private class CustomForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            throw new NotImplementedException();
        }
    }
}
