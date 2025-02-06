// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Utilities;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class StreamCopyHttpContentTests
{
    private static StreamCopyHttpContent CreateContent(HttpContext context = null, bool isStreamingRequest = false, TimeProvider timeProvider = null, ActivityCancellationTokenSource contentCancellation = null)
    {
        context ??= new DefaultHttpContext();
        timeProvider ??= TimeProvider.System;
        contentCancellation ??= ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        return new StreamCopyHttpContent(context, isStreamingRequest, timeProvider, NullLogger.Instance, contentCancellation);
    }

    [Fact]
    public async Task CopyToAsync_InvokesStreamCopier()
    {
        const int SourceSize = (128 * 1024) - 3;

        var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();

        var sut = CreateContent(context);

        Assert.False(sut.ConsumptionTask.IsCompleted);
        Assert.False(sut.Started);
        await sut.CopyToWithCancellationAsync(destination);

        Assert.True(sut.Started);
        Assert.True(sut.ConsumptionTask.IsCompleted);
        Assert.Equal(sourceBytes, destination.ToArray());
    }

    [Theory]
    [InlineData(false)] // we expect to always flush at least once to trigger sending request headers
    [InlineData(true)]
    public async Task CopyToAsync_AutoFlushing(bool autoFlush)
    {
        // Must be same as StreamCopier constant.
        const int DefaultBufferSize = 65536;
        const int SourceSize = (128 * 1024) - 3;

        var expectedFlushes = 0;
        if (autoFlush)
        {
            // How many buffers is needed to send the source rounded up.
            expectedFlushes = (SourceSize - 1) / DefaultBufferSize + 1;
        }
        // Explicit flush after headers are sent.
        expectedFlushes++;

        var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        var flushCountingDestination = new FlushCountingStream(destination);

        var sut = CreateContent(context, autoFlush);

        Assert.False(sut.ConsumptionTask.IsCompleted);
        Assert.False(sut.Started);
        await sut.CopyToWithCancellationAsync(flushCountingDestination);

        Assert.True(sut.Started);
        Assert.True(sut.ConsumptionTask.IsCompleted);
        Assert.Equal(sourceBytes, destination.ToArray());
        Assert.Equal(expectedFlushes, flushCountingDestination.NumFlushes);
    }

    [Fact]
    public async Task CopyToAsync_AsyncSequencing()
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Mock<Stream>();
        source.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>())).Returns(() => new ValueTask<int>(tcs.Task));
        var context = new DefaultHttpContext();
        context.Request.Body = source.Object;
        var destination = new MemoryStream();

        var sut = CreateContent(context);

        Assert.False(sut.ConsumptionTask.IsCompleted);
        Assert.False(sut.Started);
        var task = sut.CopyToWithCancellationAsync(destination);

        Assert.True(sut.Started); // This should happen synchronously
        Assert.False(sut.ConsumptionTask.IsCompleted); // This cannot happen until the tcs releases it

        tcs.TrySetResult(0);
        await task;
        Assert.True(sut.ConsumptionTask.IsCompleted);
    }

    [Fact]
    public Task ReadAsStreamAsync_Throws()
    {
        var sut = CreateContent();

        Func<Task> func = () => sut.ReadAsStreamAsync();

        return Assert.ThrowsAsync<NotImplementedException>(func);
    }

    [Fact]
    public void AllowDuplex_ReturnsTrue()
    {
        var sut = CreateContent();

        // This is an internal property that HttpClient and friends use internally and which must be true
        // to support duplex channels. This test helps detect regressions or changes in undocumented behavior
        // in .NET Core, and it passes as of .NET Core 3.1.
        var allowDuplexProperty = typeof(HttpContent).GetProperty("AllowDuplex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(allowDuplexProperty);
        var allowDuplex = (bool)allowDuplexProperty.GetValue(sut);
        Assert.True(allowDuplex);
    }

    [Fact]
    public async Task SerializeToStreamAsync_RespectsContentCancellation()
    {
        var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = new ReadDelegatingStream(new MemoryStream(), async (buffer, cancellation) =>
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            Assert.False(cancellation.IsCancellationRequested);
            await tcs.Task;
            Assert.True(cancellation.IsCancellationRequested);
            return 0;
        });

        var context = new DefaultHttpContext();
        context.Request.Body = source;

        using var contentCts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);

        var sut = CreateContent(context, contentCancellation: contentCts);

        var copyToTask = sut.CopyToWithCancellationAsync(new MemoryStream());
        contentCts.Cancel();
        tcs.SetResult(0);

        await copyToTask;
    }

    [Fact]
    public async Task SerializeToStreamAsync_CanBeCanceledExternally()
    {
        var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = new ReadDelegatingStream(new MemoryStream(), async (buffer, cancellation) =>
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            Assert.False(cancellation.IsCancellationRequested);
            await tcs.Task;
            Assert.True(cancellation.IsCancellationRequested);
            return 0;
        });

        var context = new DefaultHttpContext();
        context.Request.Body = source;

        var sut = CreateContent(context);

        using var cts = new CancellationTokenSource();
        var copyToTask = sut.CopyToAsync(new MemoryStream(), cts.Token);
        cts.Cancel();
        tcs.SetResult(0);

        await copyToTask;
    }

    private class FlushCountingStream : DelegatingStream
    {
        public FlushCountingStream(Stream stream)
            : base(stream)
        { }

        public int NumFlushes { get; private set; }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await base.FlushAsync(cancellationToken);
            NumFlushes++;
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
