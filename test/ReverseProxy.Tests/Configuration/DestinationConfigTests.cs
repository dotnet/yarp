// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests;

public class DestinationConfigTests
{
    [Fact]
    public void Equals_Same_Value_Returns_True()
    {
        var options1 = new DestinationConfig
        {
            Address = "https://localhost:10000/destA",
            Health = "https://localhost:20000/destA",
            Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
        };

        var options2 = new DestinationConfig
        {
            Address = "https://localhost:10000/DestA",
            Health = "https://localhost:20000/DestA",
            Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
        };

        var options3 = options1 with { }; // Clone

        Assert.True(options1.Equals(options2));
        Assert.True(options1.Equals(options3));
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
        Assert.Equal(options1.GetHashCode(), options3.GetHashCode());
    }

    [Fact]
    public void Equals_Different_Value_Returns_False()
    {

        var options1 = new DestinationConfig
        {
            Address = "https://localhost:10000/destA",
            Health = "https://localhost:20000/destA",
            Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
        };

        Assert.False(options1.Equals(options1 with { Address = "different" }));
        Assert.False(options1.Equals(options1 with { Health = null }));
        Assert.False(options1.Equals(options1 with
        {
            Metadata = new Dictionary<string, string>
            {
                { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" }
            }
        }));
    }

    [Fact]
    public void Equals_Second_Null_Returns_False()
    {
        var options1 = new DestinationConfig();

        var equals = options1.Equals(null);

        Assert.False(equals);
    }
}
