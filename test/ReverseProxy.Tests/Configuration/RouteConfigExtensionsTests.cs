// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

public class RouteConfigExtensionsTests
{
    [Fact]
    public void Equals_Positive()
    {
        var a = new RouteConfig {
            RouteId = "r1",
            Extensions = new Dictionary<Type, IConfigExtension> {
                {
                    typeof(AB),
                    new AB {
                        AbTests = new List<ABTest> {
                            new() { ClusterId = "C1", Weight = 80 }, new() { ClusterId = "C2", Weight = 10 }
                        }
                    }
                }
            }
        };

        var b = new RouteConfig {
            RouteId = "r1",
            Extensions = new Dictionary<Type, IConfigExtension> {
                {
                    typeof(AB),
                    new AB {
                        AbTests = new List<ABTest> {
                            new() { ClusterId = "C1", Weight = 80 }, new() { ClusterId = "C2", Weight = 10 }
                        }
                    }
                }
            }
        };

        var aExtension = a.GetExtension<AB>();
        var bExtension = b.GetExtension<AB>();

        Assert.True(a.Equals(b));
        Assert.True(aExtension.Equals(bExtension));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(aExtension.GetHashCode(),aExtension.GetHashCode());
    }

    [Fact]
    public void Equals_Negative()
    {
        var a = new RouteConfig {
            RouteId = "r1",
            Extensions = new Dictionary<Type, IConfigExtension> {
                {
                    typeof(AB),
                    new AB {
                        AbTests = new List<ABTest>() {
                            new() { ClusterId = "C1", Weight = 80 }, new() { ClusterId = "C2", Weight = 10 }
                        }
                    }
                }
            }
        };

        var b = new RouteConfig {
            RouteId = "r1",
            Extensions = new Dictionary<Type, IConfigExtension> {
                {
                    typeof(AB),
                    new AB {
                        AbTests = new List<ABTest>() {
                            new() { ClusterId = "C1", Weight = 80 }, new() { ClusterId = "C3", Weight = 10 }
                        }
                    }
                }
            }
        };

        var aExtension = a.GetExtension<AB>();
        var bExtension = b.GetExtension<AB>();

        Assert.False(a.Equals(b));
        Assert.False(aExtension.Equals(bExtension));
    }
}

public class AB:IConfigExtension
{
    public List<ABTest> AbTests { get; set; }

    public override bool Equals(object obj)
    {
        return obj is AB other && CollectionEqualityHelper.Equals(AbTests, other.AbTests);
    }

    public override int GetHashCode()
    {
        return CollectionEqualityHelper.GetHashCode(AbTests);
    }
}

public class ABTest
{
    public string ClusterId { get; set; }
    public double Weight { get; set; }

    public override bool Equals(object obj)
    {
        return obj is ABTest other && Equals(other);
    }

    public bool Equals(ABTest other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(ClusterId, other.ClusterId, StringComparison.OrdinalIgnoreCase)
               && Weight == other.Weight;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClusterId?.GetHashCode(StringComparison.OrdinalIgnoreCase), Weight);
    }
}
