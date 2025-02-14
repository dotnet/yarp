// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class ForwarderHttpClientFactoryTests : TestAutoMockBase
{
    [Fact]
    public void Constructor_Works()
    {
        new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
    }

    [Fact]
    public void CreateClient_Works()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);

        var actual1 = factory.CreateClient(new ForwarderHttpClientContext()
        {
            NewConfig = HttpClientConfig.Empty,
            OldConfig = HttpClientConfig.Empty
        });
        var actual2 = factory.CreateClient(new ForwarderHttpClientContext()
        {
            NewConfig = HttpClientConfig.Empty,
            OldConfig = HttpClientConfig.Empty
        });

        Assert.NotNull(actual1);
        Assert.NotNull(actual2);
        Assert.NotSame(actual2, actual1);
    }

    [Fact]
    public void CreateClient_ApplySslProtocols_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslOptions.EnabledSslProtocols);
        VerifyDefaultValues(handler, "SslProtocols");
    }

    [Fact]
    public void CreateClient_ApplyDangerousAcceptAnyServerCertificate_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig { DangerousAcceptAnyServerCertificate = true };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.NotNull(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.True(handler.SslOptions.RemoteCertificateValidationCallback(default, default, default, default));
        VerifyDefaultValues(handler, "DangerousAcceptAnyServerCertificate");
    }

    [Fact]
    public void CreateClient_ApplyMaxConnectionsPerServer_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig { MaxConnectionsPerServer = 22 };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.Equal(22, handler.MaxConnectionsPerServer);
        VerifyDefaultValues(handler, "MaxConnectionsPerServer");
    }

    [Fact]
    public void CreateClient_ApplyWebProxy_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig
        {
            WebProxy = new WebProxyConfig()
            {
                Address = new Uri("http://localhost:8080"),
                BypassOnLocal = true,
                UseDefaultCredentials = true
            }
        };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.NotNull(handler.Proxy);
        Assert.True(handler.UseProxy);
        VerifyDefaultValues(handler, "WebProxy");
    }

    [Fact]
    public void CreateClient_ApplyRequestHeaderEncoding_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig
        {
            RequestHeaderEncoding = Encoding.Latin1.WebName
        };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.NotNull(handler.RequestHeaderEncodingSelector);
        Assert.Equal(Encoding.Latin1, handler.RequestHeaderEncodingSelector(default, default));
        VerifyDefaultValues(handler, nameof(SocketsHttpHandler.RequestHeaderEncodingSelector));
    }

    [Fact]
    public void CreateClient_ApplyResponseHeaderEncoding_Success()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig
        {
            ResponseHeaderEncoding = Encoding.Latin1.WebName
        };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.NotNull(handler);
        Assert.NotNull(handler.ResponseHeaderEncodingSelector);
        Assert.Equal(Encoding.Latin1, handler.ResponseHeaderEncodingSelector(default, default));
        VerifyDefaultValues(handler, nameof(SocketsHttpHandler.ResponseHeaderEncodingSelector));
    }

    [Fact]
    public void CreateClient_OldClientExistsNoConfigChange_ReturnsOldInstance()
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
        var oldOptions = new HttpClientConfig
        {
            SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
            DangerousAcceptAnyServerCertificate = true,
            MaxConnectionsPerServer = 10,
            RequestHeaderEncoding = Encoding.Latin1.WebName,
        };
        var newOptions = oldOptions with { }; // Clone
        var oldMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var newMetadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var context = new ForwarderHttpClientContext { ClusterId = "cluster1", OldConfig = oldOptions, OldMetadata = oldMetadata, OldClient = oldClient, NewConfig = newOptions, NewMetadata = newMetadata };

        var actualClient = factory.CreateClient(context);

        Assert.Equal(newOptions, oldOptions);
        Assert.Same(oldClient, actualClient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateClient_ApplyEnableMultipleHttp2Connections_Success(bool enableMultipleHttp2Connections)
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var options = new HttpClientConfig { EnableMultipleHttp2Connections = enableMultipleHttp2Connections };
        var client = factory.CreateClient(new ForwarderHttpClientContext { NewConfig = options });

        var handler = GetHandler(client);

        Assert.Equal(enableMultipleHttp2Connections, handler.EnableMultipleHttp2Connections);
    }

    [Theory]
    [MemberData(nameof(GetChangedHttpClientOptions))]
    public void CreateClient_OldClientExistsHttpClientOptionsChanged_ReturnsNewInstance(HttpClientConfig oldOptions, HttpClientConfig newOptions)
    {
        var factory = new ForwarderHttpClientFactory(Mock<ILogger<ForwarderHttpClientFactory>>().Object);
        var oldClient = new HttpMessageInvoker(new SocketsHttpHandler());
        var context = new ForwarderHttpClientContext { ClusterId = "cluster1", OldConfig = oldOptions, OldClient = oldClient, NewConfig = newOptions };

        var actualClient = factory.CreateClient(context);

        Assert.NotEqual(newOptions, oldOptions);
        Assert.NotSame(oldClient, actualClient);
    }

    public static IEnumerable<object[]> GetChangedHttpClientOptions()
    {
        return new[]
        {
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = false,
                    MaxConnectionsPerServer = null,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = false,
                    MaxConnectionsPerServer = null,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = false,
                    MaxConnectionsPerServer = null,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = null,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 20,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    EnableMultipleHttp2Connections = true
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    EnableMultipleHttp2Connections = false
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.Latin1.WebName,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                    ResponseHeaderEncoding = Encoding.UTF8.WebName,
                },
            },
            new object[] {
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                    ResponseHeaderEncoding = Encoding.Latin1.WebName,
                },
                new HttpClientConfig
                {
                    SslProtocols = SslProtocols.Tls11,
                    DangerousAcceptAnyServerCertificate = true,
                    MaxConnectionsPerServer = 10,
                    RequestHeaderEncoding = Encoding.UTF8.WebName,
                    ResponseHeaderEncoding = Encoding.UTF8.WebName,
                },
            }
        };
    }

    public static SocketsHttpHandler GetHandler(HttpMessageInvoker client)
    {
        var handlerFieldInfo = typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.Name == "_handler");
        var handler = handlerFieldInfo.GetValue(client);
        return (SocketsHttpHandler)handler;
    }

    private void VerifyDefaultValues(SocketsHttpHandler actualHandler, params string[] skippedExtractors)
    {
        var skippedSet = new HashSet<string>(skippedExtractors);
        var defaultHandler = new SocketsHttpHandler();
        foreach (var extractor in GetAllExtractors().Where(e => !skippedSet.Contains(e.name)).Select(e => e.extractor))
        {
            Assert.Equal(extractor(defaultHandler), extractor(actualHandler));
        }
    }

    private (string name, Func<SocketsHttpHandler, object> extractor)[] GetAllExtractors()
    {
        return new (string name, Func<SocketsHttpHandler, object> extractor)[] {
            ("SslProtocols", h => h.SslOptions.EnabledSslProtocols),
            ("DangerousAcceptAnyServerCertificate", h => h.SslOptions.RemoteCertificateValidationCallback),
            ("ClientCertificate", h => h.SslOptions.ClientCertificates),
            ("MaxConnectionsPerServer", h => h.MaxConnectionsPerServer),
            ("WebProxy", h => h.Proxy)
        };
    }
}
