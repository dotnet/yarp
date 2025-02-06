// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Utilities.Tests;

public class ActivityCancellationTokenSourceTests
{
    [Fact]
    public void ActivityCancellationTokenSource_PoolsSources()
    {
        HashSet<ActivityCancellationTokenSource> sources = [];

        for (var i = 0; i < 1_000; i++)
        {
            var source = ActivityCancellationTokenSource.Rent(TimeSpan.FromMinutes(10), CancellationToken.None);
            source.Return();
            sources.Add(source);
        }

        Assert.True(sources.Count < 1000);
    }

    [Fact]
    public void ActivityCancellationTokenSource_DoesNotPoolsCanceledSources()
    {
        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);
        cts.Cancel();

        var cts2 = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.NotSame(cts, cts2);
    }

    [Fact]
    public void ActivityCancellationTokenSource_RespectsLinkedToken1()
    {
        var linkedCts = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), linkedCts.Token);
        linkedCts.Cancel();

        Assert.True(cts.CancelledByLinkedToken);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ActivityCancellationTokenSource_RespectsLinkedToken2()
    {
        var linkedCts = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), default, linkedCts.Token);
        linkedCts.Cancel();

        Assert.True(cts.CancelledByLinkedToken);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ActivityCancellationTokenSource_RespectsBothLinkedTokens()
    {
        var linkedCts1 = new CancellationTokenSource();
        var linkedCts2 = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), linkedCts1.Token, linkedCts2.Token);
        linkedCts1.Cancel();
        linkedCts2.Cancel();

        Assert.True(cts.CancelledByLinkedToken);
        Assert.True(cts.IsCancellationRequested);
    }

    [Fact]
    public void ActivityCancellationTokenSource_ClearsRegistrations()
    {
        var linkedCts1 = new CancellationTokenSource();
        var linkedCts2 = new CancellationTokenSource();

        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromSeconds(10), linkedCts1.Token, linkedCts2.Token);
        cts.Return();

        linkedCts1.Cancel();
        linkedCts2.Cancel();

        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public async Task ActivityCancellationTokenSource_RespectsTimeout()
    {
        var cts = ActivityCancellationTokenSource.Rent(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        for (var i = 0; i < 1000; i++)
        {
            if (cts.IsCancellationRequested)
            {
                Assert.False(cts.CancelledByLinkedToken);
                return;
            }

            await Task.Delay(1);
        }

        Assert.Fail("Cts was not canceled");
    }
}
