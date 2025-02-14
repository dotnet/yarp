// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class HealthCheckConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var options1 = new HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "Passive",
                ReactivationPeriod = TimeSpan.FromSeconds(5),
            }
        };

        var options2 = new HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "any5xxResponse",
                Path = "/a",
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "passive",
                ReactivationPeriod = TimeSpan.FromSeconds(5),
            }
        };

        var equals = options1.Equals(options2);

        Assert.True(equals);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {
        var options1 = new HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Any5xxResponse",
                Path = "/a",
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "Passive",
                ReactivationPeriod = TimeSpan.FromSeconds(5),
            }
        };

        var options2 = new HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(2),
                Timeout = TimeSpan.FromSeconds(1),
                Policy = "Different",
                Path = "/a",
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "Passive",
                ReactivationPeriod = TimeSpan.FromSeconds(5),
            }
        };

        var equals = options1.Equals(options2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var options1 = new HealthCheckConfig();

        var equals = options1.Equals(null);

        Assert.False(equals);
    }
}
