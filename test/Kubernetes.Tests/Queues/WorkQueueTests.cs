// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.Kubernetes.Controller.Queues.Tests;

public class WorkQueueTests
{
    public CancellationTokenSource Cancellation { get; set; } = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    [Fact]
    public async Task NormalUsageIsAddGetDone()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        Assert.Equal(0, queue.Len());
        queue.Add("one");
        Assert.Equal(1, queue.Len());
        queue.Add("two");
        Assert.Equal(2, queue.Len());
        var (item1, shutdown1) = await queue.GetAsync(Cancellation.Token);
        Assert.Equal(1, queue.Len());
        queue.Done(item1);
        Assert.Equal(1, queue.Len());
        var (item2, shutdown2) = await queue.GetAsync(Cancellation.Token);
        Assert.Equal(0, queue.Len());
        queue.Done(item2);
        Assert.Equal(0, queue.Len());

        Assert.Equal("one", item1);
        Assert.False(shutdown1);
        Assert.Equal("two", item2);
        Assert.False(shutdown2);
    }

    [Fact]
    public void AddingSameItemAgainHasNoEffect()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var len1 = queue.Len();
        queue.Add("one");
        var len2 = queue.Len();
        queue.Add("one");
        var len3 = queue.Len();

        Assert.Equal(0, len1);
        Assert.Equal(1, len2);
        Assert.Equal(1, len3);
    }

    [Fact]
    public async Task CallingAddWhileItemIsBeingProcessedHasNoEffect()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var lenOriginal = queue.Len();
        queue.Add("one");
        var lenAfterAdd = queue.Len();
        var (item1, _) = await queue.GetAsync(Cancellation.Token);
        var lenAfterGet = queue.Len();
        queue.Add("one");
        var lenAfterAddAgain = queue.Len();

        Assert.Equal("one", item1);
        Assert.Equal(0, lenOriginal);
        Assert.Equal(1, lenAfterAdd);
        Assert.Equal(0, lenAfterGet);
        Assert.Equal(0, lenAfterAddAgain);

        Assert.Equal(0, queue.Len());
    }

    [Fact]
    public async Task ItemCanBeAddedAgainAfterDoneIsCalled()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var lenOriginal = queue.Len();
        queue.Add("one");
        var lenAfterAdd = queue.Len();
        var (item1, _) = await queue.GetAsync(Cancellation.Token);
        var lenAfterGet = queue.Len();
        queue.Done(item1);
        var lenAfterDone = queue.Len();
        queue.Add("one");
        var lenAfterAddAgain = queue.Len();

        Assert.Equal("one", item1);
        Assert.Equal(0, lenOriginal);
        Assert.Equal(1, lenAfterAdd);
        Assert.Equal(0, lenAfterGet);
        Assert.Equal(0, lenAfterDone);
        Assert.Equal(1, lenAfterAddAgain);

        Assert.Equal(1, queue.Len());
    }

    [Fact]
    public async Task IfAddWasCalledDuringProcessingThenItemIsRequeuedByDone()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var lenOriginal = queue.Len();
        queue.Add("one");
        var lenAfterAdd = queue.Len();
        var (item1, _) = await queue.GetAsync(Cancellation.Token);
        var lenAfterGet = queue.Len();
        queue.Add("one");
        var lenAfterAddAgain = queue.Len();
        queue.Done(item1);
        var lenAfterDone = queue.Len();
        var (item2, _) = await queue.GetAsync(Cancellation.Token);
        var lenAfterGetAgain = queue.Len();

        Assert.Equal("one", item1);
        Assert.Equal("one", item2);
        Assert.Equal(0, lenOriginal);
        Assert.Equal(1, lenAfterAdd);
        Assert.Equal(0, lenAfterGet);
        Assert.Equal(0, lenAfterAddAgain);
        Assert.Equal(1, lenAfterDone);
        Assert.Equal(0, lenAfterGetAgain);

        Assert.Equal(0, queue.Len());
    }

    [Fact]
    public async Task GetCompletesOnceAddIsCalled()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var getTask = queue.GetAsync(Cancellation.Token);
        Assert.Equal(0, queue.Len());
        Assert.False(getTask.IsCompleted);

        queue.Add("one");
        var (item1, _) = await getTask;
        Assert.Equal(0, queue.Len());
        Assert.True(getTask.IsCompleted);

        Assert.Equal("one", item1);
        Assert.Equal(0, queue.Len());
    }

    [Fact]
    public async Task GetReturnsShutdownTrueAfterShutdownIsCalled()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var getTask = queue.GetAsync(Cancellation.Token);
        Assert.Equal(0, queue.Len());
        Assert.False(getTask.IsCompleted);

        queue.ShutDown();
        var (item1, shutdown1) = await getTask;
        Assert.Equal(0, queue.Len());
        Assert.True(getTask.IsCompleted);

        Assert.True(shutdown1);
        Assert.Equal(0, queue.Len());
    }

    [Fact]
    public void ShuttingDownReturnsTrueAfterShutdownIsCalled()
    {
        using IWorkQueue<string> queue = new WorkQueue<string>();

        var shuttingDownBefore = queue.ShuttingDown();
        queue.ShutDown();
        var shuttingDownAfter = queue.ShuttingDown();

        Assert.False(shuttingDownBefore);
        Assert.True(shuttingDownAfter);
    }
}
