// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class SessionAffinityConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var options1 = new SessionAffinityConfig
        {
            Enabled = true,
            FailurePolicy = "policy1",
            Policy = "policy1",
            AffinityKeyName = "Key1"
        };

        var options2 = new SessionAffinityConfig
        {
            Enabled = true,
            FailurePolicy = "Policy1",
            Policy = "Policy1",
            AffinityKeyName = "Key1"
        };

        var equals = options1.Equals(options2);

        Assert.True(equals);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {
        var options1 = new SessionAffinityConfig
        {
            Enabled = true,
            FailurePolicy = "policy1",
            Policy = "policy1",
            AffinityKeyName = "Key1"
        };

        var options2 = new SessionAffinityConfig
        {
            Enabled = false,
            FailurePolicy = "policy2",
            Policy = "policy2",
            AffinityKeyName = "Key1"
        };

        var equals = options1.Equals(options2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var options1 = new SessionAffinityConfig
        {
            Enabled = true,
            FailurePolicy = "policy1",
            Policy = "policy1",
            AffinityKeyName = "Key1"
        };

        var equals = options1.Equals(null);

        Assert.False(equals);
    }
}
