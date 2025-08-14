// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy;

public class HttpForwarderCancellationTests
{
    // HTTP/2 over TLS is not supported on macOS due to missing ALPN support.
    // See https://github.com/dotnet/runtime/issues/27727
    public static bool Http2OverTlsSupported => !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact(Skip = "Condition not met", SkipUnless = nameof(Http2OverTlsSupported))]
    public async Task ServerSendsHttp2Reset_ReadToClientIsCanceled()
    {
        var readAsyncCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var test = new TestEnvironment(
            async context =>
            {
                Assert.Equal("HTTP/2", context.Request.Protocol);

                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("Hello"));
                await context.Response.CompleteAsync();

                await readAsyncCalled.Task;

                var resetFeature = context.Features.Get<IHttpResetFeature>();
                Assert.NotNull(resetFeature);
                resetFeature.Reset(0); // NO_ERROR
            })
        {
            UseHttpsOnDestination = true,
            UseHttpsOnProxy = true,
            ConfigureProxyApp = proxyApp =>
            {
                proxyApp.Use(next => context =>
                {
                    context.Request.Body = new ReadDelegatingStream(context.Request.Body, async (memory, cancellation) =>
                    {
                        Assert.False(cancellation.IsCancellationRequested);
                        readAsyncCalled.SetResult();

                        var startTime = DateTime.UtcNow;
                        while (DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(10))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            await Task.Delay(10, cancellation);
                        }

                        throw new InvalidOperationException("Cancellation was not requested");
                    });

                    return next(context);
                });
            },
        };

        await test.Invoke(async uri =>
        {
            var content = new InfiniteHttpContent();

            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Version = HttpVersion.Version20,
                Content = content
            };

            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            using var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("Hello", responseString);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => content.Completion.Task);
        });
    }

    private sealed class InfiniteHttpContent : HttpContent
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            throw new NotImplementedException();
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            new Random(42).NextBytes(buffer);

            while (true)
            {
                try
                {
                    await stream.WriteAsync(buffer, cancellationToken);
                }
                catch (Exception ex)
                {
                    Completion.SetException(ex);
                    return;
                }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class ReadDelegatingStream : DelegatingStream
    {
        private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> _readAsync;

        public ReadDelegatingStream(Stream stream, Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync)
            : base(stream)
        {
            _readAsync = readAsync;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _readAsync(buffer, cancellationToken);
        }
    }
}
