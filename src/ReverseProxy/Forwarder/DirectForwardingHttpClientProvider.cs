// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Forwarder;

internal sealed class DirectForwardingHttpClientProvider
{
    public HttpMessageInvoker HttpClient { get; }

    public DirectForwardingHttpClientProvider() : this(new ForwarderHttpClientFactory()) { }

    public DirectForwardingHttpClientProvider(IForwarderHttpClientFactory factory)
    {
        HttpClient = factory.CreateClient(new ForwarderHttpClientContext
        {
            NewConfig = HttpClientConfig.Empty
        });
    }
}
