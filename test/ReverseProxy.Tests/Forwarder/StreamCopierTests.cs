// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class StreamCopierTests : TestAutoMockBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CopyAsync_Works(bool isRequest)
    {
        var events = TestEventListener.Collect();

        const int SourceSize = (128 * 1024) - 3;
        var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();

        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        await StreamCopier.CopyAsync(isRequest, source, destination, SourceSize, TimeProvider.System, cts, cts.Token);

        Assert.False(cts.Token.IsCancellationRequested);

        Assert.Equal(sourceBytes, destination.ToArray());

        AssertContentTransferred(events, isRequest, SourceSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceThrows_Reported(bool isRequest)
    {
        var events = TestEventListener.Collect();

        var timeProvider = new TestTimeProvider();
        var sourceWaitTime = TimeSpan.FromMilliseconds(12345);
        var source = new SlowStream(new ThrowStream(), timeProvider, sourceWaitTime);
        var destination = new MemoryStream();

        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, StreamCopier.UnknownLength, timeProvider, cts, cts.Token);
        Assert.Equal(StreamCopyResult.InputError, result);
        Assert.IsAssignableFrom<IOException>(error);

        AssertContentTransferred(events, isRequest,
            contentLength: 0,
            iops: 1,
            firstReadTime: sourceWaitTime,
            readTime: sourceWaitTime,
            writeTime: TimeSpan.Zero);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DestinationThrows_Reported(bool isRequest)
    {
        var events = TestEventListener.Collect();

        const int SourceSize = 10;
        const int BytesPerRead = 3;

        var timeProvider = new TestTimeProvider();
        var sourceWaitTime = TimeSpan.FromMilliseconds(12345);
        var destinationWaitTime = TimeSpan.FromMilliseconds(42);
        var source = new SlowStream(new MemoryStream(new byte[SourceSize]), timeProvider, sourceWaitTime) { MaxBytesPerRead = BytesPerRead };
        var destination = new SlowStream(new ThrowStream(), timeProvider, destinationWaitTime);

        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, SourceSize, timeProvider, cts, cts.Token);
        Assert.Equal(StreamCopyResult.OutputError, result);
        Assert.IsAssignableFrom<IOException>(error);

        AssertContentTransferred(events, isRequest,
            contentLength: BytesPerRead,
            iops: 1,
            firstReadTime: sourceWaitTime,
            readTime: sourceWaitTime,
            writeTime: destinationWaitTime);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelled_Reported(bool isRequest)
    {
        var events = TestEventListener.Collect();

        var source = new MemoryStream(new byte[10]);
        var destination = new MemoryStream();

        var requestCts = new CancellationTokenSource();
        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), requestCts.Token);
        requestCts.Cancel();
        var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, StreamCopier.UnknownLength, new TestTimeProvider(), cts, cts.Token);
        Assert.Equal(StreamCopyResult.Canceled, result);
        Assert.IsAssignableFrom<OperationCanceledException>(error);

        AssertContentTransferred(events, isRequest,
            contentLength: 0,
            iops: 1,
            firstReadTime: TimeSpan.Zero,
            readTime: TimeSpan.Zero,
            writeTime: TimeSpan.Zero);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SlowStreams_TelemetryReportsCorrectTime(bool isRequest)
    {
        var events = TestEventListener.Collect();

        const int SourceSize = 3;
        var sourceBytes = new byte[SourceSize];
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();

        var timeProvider = new TestTimeProvider();
        var sourceWaitTime = TimeSpan.FromMilliseconds(12345);
        var destinationWaitTime = TimeSpan.FromMilliseconds(42);

        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        await StreamCopier.CopyAsync(
            isRequest,
            new SlowStream(source, timeProvider, sourceWaitTime),
            new SlowStream(destination, timeProvider, destinationWaitTime),
            SourceSize,
            timeProvider,
            cts,
            cts.Token);

        Assert.Equal(sourceBytes, destination.ToArray());

        AssertContentTransferred(events, isRequest, SourceSize,
            iops: SourceSize + 1,
            firstReadTime: sourceWaitTime,
            readTime: (SourceSize + 1) * sourceWaitTime,
            writeTime: SourceSize * destinationWaitTime);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LongContentTransfer_TelemetryReportsTransferringEvents(bool isRequest)
    {
        var events = TestEventListener.Collect();

        const int SourceSize = 123;
        var sourceBytes = new byte[SourceSize];
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();

        var timeProvider = new TestTimeProvider();
        var sourceWaitTime = TimeSpan.FromMilliseconds(789); // Every second read triggers ContentTransferring
        var destinationWaitTime = TimeSpan.FromMilliseconds(42);

        const int BytesPerRead = 3;
        var contentReads = (int)Math.Ceiling((double)SourceSize / BytesPerRead);

        using var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        await StreamCopier.CopyAsync(
            isRequest,
            new SlowStream(source, timeProvider, sourceWaitTime) { MaxBytesPerRead = BytesPerRead },
            new SlowStream(destination, timeProvider, destinationWaitTime),
            SourceSize,
            timeProvider,
            cts,
            cts.Token);

        Assert.Equal(sourceBytes, destination.ToArray());

        AssertContentTransferred(events, isRequest, SourceSize,
            iops: contentReads + 1,
            firstReadTime: sourceWaitTime,
            readTime: (contentReads + 1) * sourceWaitTime,
            writeTime: contentReads * destinationWaitTime);

        var transferringEvents = events.Where(e => e.EventName == "ContentTransferring").ToArray();
        Assert.Equal(contentReads / 2, transferringEvents.Length);

        for (var i = 0; i < transferringEvents.Length; i++)
        {
            var payload = transferringEvents[i].Payload;
            Assert.Equal(5, payload.Count);

            Assert.Equal(isRequest, (bool)payload[0]);

            var contentLength = (long)payload[1];

            var iops = (long)payload[2];
            Assert.Equal((i + 1) * 2, iops);

            if (contentLength % BytesPerRead == 0)
            {
                Assert.Equal(iops * BytesPerRead, contentLength);
            }
            else
            {
                Assert.Equal(transferringEvents.Length - 1, i);
                Assert.Equal(SourceSize, contentLength);
            }

            var readTime = new TimeSpan((long)payload[3]);
            Assert.Equal(iops * sourceWaitTime, readTime, new ApproximateTimeSpanComparer());

            var writeTime = new TimeSpan((long)payload[4]);
            Assert.Equal(iops * destinationWaitTime, writeTime, new ApproximateTimeSpanComparer());
        }
    }

    private static void AssertContentTransferred(
        List<EventWrittenEventArgs> events,
        bool isRequest,
        long contentLength,
        long? iops = null,
        TimeSpan? firstReadTime = null,
        TimeSpan? readTime = null,
        TimeSpan? writeTime = null)
    {
        var contentTransferred = Assert.Single(events, e => e.EventName == "ContentTransferred");
        var payload = contentTransferred.Payload;
        Assert.Equal(6, payload.Count);

        Assert.Equal(isRequest, (bool)payload[0]);
        Assert.Equal(contentLength, (long)payload[1]);

        var actualIops = (long)payload[2];
        if (iops.HasValue)
        {
            Assert.Equal(iops.Value, actualIops);
        }
        else
        {
            Assert.InRange(actualIops, 1, contentLength + 1);
        }

        if (readTime.HasValue)
        {
            Assert.Equal(readTime.Value, new TimeSpan((long)payload[3]), new ApproximateTimeSpanComparer());
        }

        if (writeTime.HasValue)
        {
            Assert.Equal(writeTime.Value, new TimeSpan((long)payload[4]), new ApproximateTimeSpanComparer());
        }

        if (firstReadTime.HasValue)
        {
            Assert.Equal(firstReadTime.Value, new TimeSpan((long)payload[5]), new ApproximateTimeSpanComparer());

            if (readTime.HasValue)
            {
                Assert.True(firstReadTime.Value <= readTime.Value);
            }
        }

        var stages = events.GetProxyStages();

        var startStage = isRequest ? ForwarderStage.RequestContentTransferStart : ForwarderStage.ResponseContentTransferStart;
        var startTime = Assert.Single(stages, s => s.Stage == startStage).TimeStamp;

        Assert.True(startTime <= contentTransferred.TimeStamp);
    }

    private class ThrowStream : DelegatingStream
    {
        public ThrowStream()
            : base(Stream.Null)
        { }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("Fake connection issue");
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("Fake connection issue");
        }
    }

    private class SlowStream : DelegatingStream
    {
        private readonly TimeSpan _waitTime;
        private readonly TestTimeProvider _timeProvider;

        public int MaxBytesPerRead { get; set; } = 1;

        public SlowStream(Stream innerStream, TestTimeProvider timeProvider, TimeSpan waitTime)
            : base(innerStream)
        {
            _timeProvider = timeProvider;
            _waitTime = waitTime;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return new ValueTask<int>(0);
            }

            _timeProvider.Advance(_waitTime);
            return base.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, MaxBytesPerRead)), cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _timeProvider.Advance(_waitTime);
            return base.WriteAsync(buffer, cancellationToken);
        }
    }

    private class ApproximateTimeSpanComparer : IEqualityComparer<TimeSpan>
    {
        public TimeSpan Precision { get; set; } = TimeSpan.FromMilliseconds(0.1);

        public bool Equals(TimeSpan x, TimeSpan y) => x > y
            ? x - y <= Precision
            : y - x <= Precision;

        public int GetHashCode(TimeSpan obj) => 42;
    }
}
