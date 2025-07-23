// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy;

public class HeaderTests
{
    [Fact]
    public async Task ProxyAsync_EmptyRequestHeader_Proxied()
    {
        var refererReceived = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);
        var customReceived = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);

        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;

        var test = new TestEnvironment(
            context =>
            {
                if (context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header))
                {
                    refererReceived.SetResult(header);
                }
                else
                {
                    refererReceived.SetException(new Exception($"Missing '{HeaderNames.Referer}' header in request"));
                }

                if (context.Request.Headers.TryGetValue("custom", out header))
                {
                    customReceived.SetResult(header);
                }
                else
                {
                    customReceived.SetException(new Exception($"Missing 'custom' header in request"));
                }

                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        Assert.True(context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header));
                        var value = Assert.Single(header);
                        Assert.True(StringValues.IsNullOrEmpty(value));

                        Assert.True(context.Request.Headers.TryGetValue("custom", out header));
                        value = Assert.Single(header);
                        Assert.True(StringValues.IsNullOrEmpty(value));

                        await next();
                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Referer: \r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"custom: \r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            while (true)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, count));
            }
            var response = responseBuilder.ToString();

            Assert.Null(proxyError);
            Assert.Null(unhandledError);

            Assert.StartsWith("HTTP/1.1 200 OK", response);

            Assert.True(refererReceived.Task.IsCompleted);
            var refererHeader = await refererReceived.Task;
            var referer = Assert.Single(refererHeader);
            Assert.True(StringValues.IsNullOrEmpty(referer));

            Assert.True(customReceived.Task.IsCompleted);
            var customHeader = await customReceived.Task;
            var custom = Assert.Single(customHeader);
            Assert.True(StringValues.IsNullOrEmpty(custom));
        });
    }

    [Fact]
    public async Task ProxyAsync_EmptyResponseHeader_Proxied()
    {
        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;

        var test = new TestEnvironment(
            context =>
            {
                context.Response.Headers[HeaderNames.WWWAuthenticate] = "";
                context.Response.Headers["custom"] = "";
                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        await next();

                        Assert.True(context.Response.Headers.TryGetValue(HeaderNames.WWWAuthenticate, out var header));
                        var value = Assert.Single(header);
                        Assert.True(StringValues.IsNullOrEmpty(value));

                        Assert.True(context.Response.Headers.TryGetValue("custom", out header));
                        value = Assert.Single(header);
                        Assert.True(StringValues.IsNullOrEmpty(value));

                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            while (true)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, count));
            }
            var response = responseBuilder.ToString();

            Assert.Null(proxyError);
            Assert.Null(unhandledError);

            var lines = response.Split("\r\n");
            Assert.Equal("HTTP/1.1 200 OK", lines[0]);
            // Order varies across versions.
            // Assert.Equal("Content-Length: 0", lines[1]);
            // Assert.Equal("Connection: close", lines[2]);
            // Assert.StartsWith("Date: ", lines[3]);
            // Assert.Equal("Server: Kestrel", lines[4]);
            Assert.Equal("WWW-Authenticate: ", lines[5]);
            Assert.Equal("custom: ", lines[6]);
            Assert.Equal("", lines[7]);
        });
    }

    [Theory]
    [InlineData("http://www.ěščřžýáíé.com", "utf-8")]
    [InlineData("http://www.çáéôîèñøæ.com", "iso-8859-1")]
    public async Task ProxyAsync_RequestWithEncodedHeaderValue(string headerValue, string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var tcs = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);

        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;

        var test = new TestEnvironment(
            context =>
            {
                if (context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header))
                {
                    tcs.SetResult(header);
                }
                else
                {
                    tcs.SetException(new Exception($"Missing '{HeaderNames.Referer}' header in request"));
                }
                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            HeaderEncoding = encoding,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        Assert.True(context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header));
                        var value = Assert.Single(header);
                        Assert.Equal(headerValue, value);

                        await next();
                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Referer: "));
            await stream.WriteAsync(encoding.GetBytes(headerValue));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            while (true)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, count));
            }
            var response = responseBuilder.ToString();

            Assert.Null(proxyError);
            Assert.Null(unhandledError);

            Assert.StartsWith("HTTP/1.1 200 OK", response);

            Assert.True(tcs.Task.IsCompleted);
            var refererHeader = await tcs.Task;
            var referer = Assert.Single(refererHeader);
            Assert.Equal(headerValue, referer);
        });
    }

    [Theory]
    [InlineData("http://www.ěščřžýáíé.com", "utf-8")]
    [InlineData("http://www.çáéôîèñøæ.com", "iso-8859-1")]
    public async Task ProxyAsync_ResponseWithEncodedHeaderValue(string headerValue, string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);

        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var destinationTask = Task.Run(async () =>
        {
            using var tcpClient = await tcpListener.AcceptTcpClientAsync();
            await using var stream = tcpClient.GetStream();
            var buffer = new byte[4096];
            var requestBuilder = new StringBuilder();
            while (true)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }

                requestBuilder.Append(encoding.GetString(buffer, 0, count));

                // End of the request
                if (requestBuilder.Length >= 4 &&
                    requestBuilder[^4] == '\r' && requestBuilder[^3] == '\n' &&
                    requestBuilder[^2] == '\r' && requestBuilder[^1] == '\n')
                {
                    break;
                }
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Test-Extra: pingu\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Location: "));
            await stream.WriteAsync(encoding.GetBytes(headerValue));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
        });

        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;

        var proxyTest = new TestEnvironment()
        {
            ProxyProtocol = HttpProtocols.Http1,
            HeaderEncoding = encoding,
            ClusterId = "cluster1",
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
        };

        using var proxy = proxyTest.CreateProxy($"http://{tcpListener.LocalEndpoint}");

        await proxy.StartAsync();

        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(proxy.GetAddress());

            Assert.NotNull(proxyError);
            Assert.Equal(ForwarderError.ResponseHeaders, proxyError.Error);
            var ioe = Assert.IsType<InvalidOperationException>(proxyError.Exception);
            Assert.StartsWith("Invalid non-ASCII or control character in header: ", ioe.Message);
            Assert.Null(unhandledError);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

            Assert.False(response.Headers.TryGetValues(HeaderNames.Location, out _));
            Assert.False(response.Headers.TryGetValues("Test-Extra", out _));

            await destinationTask;
        }
        finally
        {
            await proxy.StopAsync();
            tcpListener.Stop();
        }
    }

    [Fact]
    public async Task ContentLengthAndTransferEncoding_ContentLengthRemoved()
    {
        var proxyTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var appTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var test = new TestEnvironment(
            context =>
            {
                try
                {
                    Assert.Null(context.Request.ContentLength);
                    Assert.Equal("chunked", context.Request.Headers[HeaderNames.TransferEncoding]);
                    appTcs.SetResult(0);
                }
                catch (Exception ex)
                {
                    appTcs.SetException(ex);
                }
                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        // Removed by the server
                        Assert.Null(context.Request.ContentLength);
                        // Set it just to make sure YARP removes it
                        context.Request.ContentLength = 11;
                        Assert.Equal("chunked", context.Request.Headers[HeaderNames.TransferEncoding]);
                        proxyTcs.SetResult(0);
                    }
                    catch (Exception ex)
                    {
                        proxyTcs.SetException(ex);
                    }

                    await next();
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 11\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Transfer-Encoding: chunked\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"b\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Hello World\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"\r\n"));
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            while (true)
            {
                var count = await stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, count));
            }
            var response = responseBuilder.ToString();

            await proxyTcs.Task;
            await appTcs.Task;

            Assert.StartsWith("HTTP/1.1 200 OK", response);
        });
    }

    [Theory]
    [MemberData(nameof(RequestMultiHeadersData))]
    public async Task MultiValueRequestHeaders(string headerName, string[] values, string expectedValues)
    {
        var proxyTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var appTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var test = new TestEnvironment(
            context =>
            {
                try
                {
                    Assert.True(context.Request.Headers.TryGetValue(headerName, out var headerValues));
                    Assert.Single(headerValues);
                    Assert.Equal(expectedValues, headerValues);
                    appTcs.SetResult(0);
                }
                catch (Exception ex)
                {
                    appTcs.SetException(ex);
                }
                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        Assert.True(context.Request.Headers.TryGetValue(headerName, out var headerValues));
                        Assert.Equal(values.Length, headerValues.Count);
                        for (var i = 0; i < values.Length; ++i)
                        {
                            Assert.Equal(values[i], headerValues[i]);
                        }
                        proxyTcs.SetResult(0);
                    }
                    catch (Exception ex)
                    {
                        proxyTcs.SetException(ex);
                    }

                    await next();
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));

            foreach (var value in values)
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"{headerName}: {value}\r\n"));
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"\r\n"));
            var response = await new StreamReader(stream).ReadToEndAsync();

            await proxyTcs.Task;
            await appTcs.Task;

            Assert.StartsWith("HTTP/1.1 200 OK", response);
        });
    }
    public static IEnumerable<string> RequestMultiHeaderNames()
    {
        var headers = new[]
        {
            HeaderNames.Accept,
            HeaderNames.AcceptCharset,
            HeaderNames.AcceptEncoding,
            HeaderNames.AcceptLanguage,
            HeaderNames.Via
        };

        foreach (var header in headers)
        {
            yield return header;
        }
    }

    public static IEnumerable<string[]> MultiValues()
    {
        var values = new string[][] {
            new[] { "testA=A_Value", "testB=B_Value", "testC=C_Value" },
            new[] { "testA=A_Value, testB=B_Value", "testC=C_Value" },
            new[] { "testA=A_Value, testB=B_Value, testC=C_Value" },
        };

        foreach (var value in values)
        {
            yield return value;
        }
    }

    public static IEnumerable<object[]> RequestMultiHeadersData()
    {
        foreach (var header in RequestMultiHeaderNames())
        {
            foreach (var value in MultiValues())
            {
                yield return new object[] { header, value, string.Join(", ", value).TrimEnd() };
            }
        }

        // Special separator ";" for Cookie header
        foreach (var value in MultiValues())
        {
            yield return new object[] { HeaderNames.Cookie, value, string.Join("; ", value).TrimEnd() };
        }
    }

    public static IEnumerable<object[]> ResponseMultiHeadersData()
    {
        foreach (var header in ResponseMultiHeaderNames())
        {
            foreach (var value in MultiValues())
            {
                yield return new object[] { header, value, value };
            }
        }
    }

    public static IEnumerable<string> ResponseMultiHeaderNames()
    {
        var headers = new[]
        {
            HeaderNames.AcceptRanges,
            HeaderNames.Allow,
            HeaderNames.ContentEncoding,
            HeaderNames.ContentLanguage,
            HeaderNames.ContentRange,
            HeaderNames.ContentType,
            HeaderNames.SetCookie,
            HeaderNames.Via,
            HeaderNames.Warning,
            HeaderNames.WWWAuthenticate
        };

        foreach (var header in headers)
        {
            yield return header;
        }
    }

    [Theory]
    [MemberData(nameof(ResponseMultiHeadersData))]
    public async Task MultiValueResponseHeaders(string headerName, string[] values, string[] expectedValues)
    {
        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;

        var test = new TestEnvironment(
            context =>
            {
                Assert.True(context.Response.Headers.TryAdd(headerName, values));
                return Task.CompletedTask;
            })
        {
            ProxyProtocol = HttpProtocols.Http1,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        await next();

                        Assert.True(context.Response.Headers.TryGetValue(headerName, out var header));
                        Assert.Equal(values.Length, header.Count);
                        for (var i = 0; i < values.Length; ++i)
                        {
                            Assert.Equal(values[i], header[i]);
                        }

                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
        };

        await test.Invoke(async proxyUri =>
        {
            var proxyHostUri = new Uri(proxyUri);

            using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            await using var stream = tcpClient.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            var response = await new StreamReader(stream).ReadToEndAsync();

            Assert.Null(proxyError);
            Assert.Null(unhandledError);

            var lines = response.Split("\r\n");
            Assert.Equal("HTTP/1.1 200 OK", lines[0]);
            foreach (var expected in expectedValues)
            {
                Assert.Contains($"{headerName}: {expected}", lines);
            }
        });
    }
}
