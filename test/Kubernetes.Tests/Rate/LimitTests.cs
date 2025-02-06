// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Yarp.Kubernetes.Controller.Rate.Tests;

public class LimitTests
{
    [Theory]
    [InlineData(15, 1, 15)]
    [InlineData(15, 120, 1800)]
    [InlineData(15, .1, 1.5)]
    [InlineData(300, 2, 600)]
    public void TokensFromDuration(double perSecond, double durationSeconds, double tokens)
    {
        var limit = new Limit(perSecond);

        var tokensFromDuration = limit.TokensFromDuration(TimeSpan.FromSeconds(durationSeconds));

        Assert.Equal(tokens, tokensFromDuration);
    }

    [Theory]
    [InlineData(15, 1, 15)]
    [InlineData(15, 120, 1800)]
    [InlineData(15, .1, 1.5)]
    [InlineData(300, 2, 600)]
    public void DurationFromTokens(double perSecond, double durationSeconds, double tokens)
    {
        var limit = new Limit(perSecond);

        var durationFromTokens = limit.DurationFromTokens(tokens);

        Assert.Equal(TimeSpan.FromSeconds(durationSeconds), durationFromTokens);
    }
}
