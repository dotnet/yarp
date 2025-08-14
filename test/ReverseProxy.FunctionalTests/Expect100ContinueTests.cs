// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy;

public class Expect100ContinueTests
{
    // HTTP/2 over TLS is not supported on macOS due to missing ALPN support.
    // See https://github.com/dotnet/runtime/issues/27727
    public static bool Http2OverTlsSupported => !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Theory(Skip = "Condition not met", SkipUnless = nameof(Http2OverTlsSupported))]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, 200)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, 200)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, 200)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false, 200)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, 400)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, 400)]
    public async Task PostExpect100_BodyNotUploadedIfFailed(HttpProtocols proxyProtocol, HttpProtocols destProtocol, bool useContentLength, int destResponseCode)
    {
        var headerTcs = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var contentString = new string('a', 1024 * 1024 * 10);
        var test = new TestEnvironment(
            async context =>
            {
                if ((context.Request.Protocol == "HTTP/1.1" && destProtocol != HttpProtocols.Http1)
                    || (context.Request.Protocol == "HTTP/2.0" && destProtocol != HttpProtocols.Http2))
                {
                    headerTcs.SetException(new Exception($"Unexpected request protocol {context.Request.Protocol}"));
                    return;
                }
                else if (context.Request.Headers.TryGetValue(HeaderNames.Expect, out var expectHeader))
                {
                    headerTcs.SetResult(expectHeader);
                }
                else
                {
                    headerTcs.SetException(new Exception("Missing 'Expect' header in request"));
                    return;
                }

                if (destResponseCode == 200)
                {
                    // 100 response code is sent automatically on reading Body.
                    await ReadContent(context, bodyTcs, Encoding.UTF8.GetByteCount(contentString));
                }

                context.Response.StatusCode = destResponseCode;
            })
        {
            ProxyProtocol = proxyProtocol,
            UseHttpsOnDestination = true,
            UseHttpsOnProxy = true,
            ConfigTransformer = (c, r) =>
            {
                c = c with
                {
                    HttpRequest = new ForwarderRequestConfig
                    {
                        Version = destProtocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11,
                    }
                };
                return (c, r);
            },
            ConfigureProxy = proxyBuilder =>
            {
                proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory, TestForwarderHttpClientFactory>();
            },
        };

        await test.Invoke(async uri =>
        {
            await ProcessHttpRequest(new Uri(uri), proxyProtocol, contentString, useContentLength, destResponseCode, false, destResponseCode == 200);

            Assert.True(headerTcs.Task.IsCompleted);
            var expectHeader = await headerTcs.Task;
            var expectValue = Assert.Single(expectHeader);
            Assert.Equal("100-continue", expectValue);

            if (destResponseCode == 200)
            {
                Assert.True(bodyTcs.Task.IsCompleted);
                var actualString = await bodyTcs.Task;
                Assert.Equal(contentString, actualString);
            }
            else
            {
                Assert.False(bodyTcs.Task.IsCompleted);
            }
        });
    }

    [Theory(Skip = "Condition not met", SkipUnless = nameof(Http2OverTlsSupported))]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, true, 200)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, true, 200)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, true, 200)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, false, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, true, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, false, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, true, 400)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false, false, 400)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false, true, 400)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, false, 400)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, true, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, false, false, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, false, true, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, false, 400)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, true, 400)]
    public async Task PostExpect100_ResponseWithPayload(HttpProtocols proxyProtocol, HttpProtocols destProtocol, bool useContentLength, bool cancelResponse, int responseCode)
    {
        var requestBodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var contentString = new string('a', 1024 * 1024 * 10);
        var test = new TestEnvironment(
            async context =>
            {
                await ReadContent(context, requestBodyTcs, Encoding.UTF8.GetByteCount(contentString));

                context.Response.StatusCode = responseCode;

                var responseBody = Encoding.UTF8.GetBytes(contentString + "Response");
                if (useContentLength)
                {
                    context.Response.Headers.ContentLength = responseBody.Length;
                }

                if (cancelResponse)
                {
                    await context.Response.BodyWriter.WriteAsync(responseBody.AsMemory(0, responseBody.Length / 2));
                    context.Abort();
                }
                else
                {
                    await context.Response.Body.WriteAsync(responseBody.AsMemory());
                }
            })
        {
            ProxyProtocol = proxyProtocol,
            UseHttpsOnDestination = true,
            UseHttpsOnProxy = true,
            ConfigTransformer = (c, r) =>
            {
                c = c with
                {
                    HttpRequest = new ForwarderRequestConfig
                    {
                        Version = destProtocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11,
                    }
                };
                return (c, r);
            },
            ConfigureProxy = proxyBuilder =>
            {
                proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory, TestForwarderHttpClientFactory>();
            },
        };

        await test.Invoke(async uri =>
        {
            await ProcessHttpRequest(new Uri(uri), proxyProtocol, contentString, useContentLength, responseCode, cancelResponse, true, async response =>
            {
                Assert.Equal(responseCode, (int)response.StatusCode);

                var actualResponse = await response.Content.ReadAsStringAsync();
                Assert.Equal(contentString + "Response", actualResponse);
            });
        });
    }

    [Theory(Skip = "Condition not met", SkipUnless = nameof(Http2OverTlsSupported))]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, false)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http1, false)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true)]
    [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true)]
    [InlineData(HttpProtocols.Http2, HttpProtocols.Http1, true)]
    public async Task PostExpect100_SkipRequestBodyWithUnsuccessfulResponseCode(HttpProtocols proxyProtocol, HttpProtocols destProtocol, bool useContentLength)
    {
        var requestBodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var contentString = new string('a', 1024 * 1024 * 10);
        var test = new TestEnvironment(
            async context =>
            {
                context.Response.StatusCode = 400;

                var responseBody = Encoding.UTF8.GetBytes(contentString + "Response");
                if (useContentLength)
                {
                    context.Response.Headers.ContentLength = responseBody.Length;
                }

                await context.Response.Body.WriteAsync(responseBody.AsMemory());
            })
        {
            ProxyProtocol = proxyProtocol,
            UseHttpsOnDestination = true,
            UseHttpsOnProxy = true,
            ConfigTransformer = (c, r) =>
            {
                c = c with
                {
                    HttpRequest = new ForwarderRequestConfig
                    {
                        Version = destProtocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11,
                    }
                };
                return (c, r);
            },
            ConfigureProxy = proxyBuilder =>
            {
                proxyBuilder.Services.AddSingleton<IForwarderHttpClientFactory, TestForwarderHttpClientFactory>();
            },
        };

        await test.Invoke(async uri =>
        {
            await ProcessHttpRequest(new Uri(uri), proxyProtocol, contentString, useContentLength, 400, cancelResponse: false, contentRead: false, async response =>
            {
                Assert.Equal(400, (int)response.StatusCode);

                var actualResponse = await response.Content.ReadAsStringAsync();
                Assert.Equal(contentString + "Response", actualResponse);
            });
        });
    }

    private static async Task ReadContent(Microsoft.AspNetCore.Http.HttpContext context, TaskCompletionSource<string> bodyTcs, int byteCount)
    {
        try
        {
            var buffer = new byte[byteCount];
            var readCount = 0;
            var totalReadCount = 0;
            do
            {
                readCount = await context.Request.Body.ReadAsync(buffer, totalReadCount, buffer.Length - totalReadCount);
                totalReadCount += readCount;
            } while (readCount != 0);

            var actualString = Encoding.UTF8.GetString(buffer);
            bodyTcs.SetResult(actualString);
        }
        catch (Exception e)
        {
            bodyTcs.SetException(e);
        }
    }

    private async Task ProcessHttpRequest(
        Uri proxyHostUri,
        HttpProtocols protocol,
        string contentString,
        bool useContentLength,
        int expectedCode,
        bool cancelResponse,
        bool contentRead,
        Func<HttpResponseMessage, Task> responseAction = null)
    {
        using var handler = new SocketsHttpHandler() { Expect100ContinueTimeout = TimeSpan.FromSeconds(60) };
        handler.UseProxy = false;
        handler.AllowAutoRedirect = false;
        handler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
        using var client = new HttpClient(handler);
        using var message = new HttpRequestMessage(HttpMethod.Post, proxyHostUri);
        message.Version = protocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11;
        message.Headers.ExpectContinue = true;

        var content = Encoding.UTF8.GetBytes(contentString);
        using var contentStream = new MemoryStream(content);
        message.Content = new StreamContent(contentStream);
        if (useContentLength)
        {
            message.Content.Headers.ContentLength = content.Length;
        }
        else
        {
            message.Headers.TransferEncodingChunked = true;
        }

        if (!cancelResponse)
        {
            using var response = await client.SendAsync(message);

            Assert.Equal(expectedCode, (int)response.StatusCode);
            if (contentRead)
            {
                Assert.Equal(content.Length, contentStream.Position);
            }
            else
            {
                Assert.Equal(0, contentStream.Position);
            }

            if (responseAction is not null)
            {
                await responseAction(response);
            }
        }
        else
        {
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(message));
            Assert.IsAssignableFrom<IOException>(exception.InnerException);
            Assert.Equal(content.Length, contentStream.Position);
        }
    }

    private class TestForwarderHttpClientFactory : ForwarderHttpClientFactory
    {
        protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
        {
            base.ConfigureHandler(context, handler);
            handler.Expect100ContinueTimeout = TimeSpan.FromSeconds(60);
        }
    }
}
