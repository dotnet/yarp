// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Yarp.ReverseProxy.Configuration;

public class ClusterConfigExtensionsTests
{
    [Fact]
    public void Equals_Positive()
    {
        var a = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Extensions =
                new Dictionary<Type, IConfigExtension>
                {
                    { typeof(UserModel), new UserModel { UserName = "admin" } }
                },
        };

        var b = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Extensions =
                new Dictionary<Type, IConfigExtension>
                {
                    { typeof(UserModel), new UserModel { UserName = "admin" } }
                }
        };

        var aExtension = a.GetExtension<UserModel>();
        var bExtension = b.GetExtension<UserModel>();

        Assert.True(a.Equals(b));
        Assert.True(aExtension.Equals(bExtension));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(aExtension.GetHashCode(), aExtension.GetHashCode());
    }

    [Fact]
    public void Equals_Negative()
    {
        var a = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Extensions =
                new Dictionary<Type, IConfigExtension>
                {
                    { typeof(UserModel), new UserModel { UserName = "admin" } }
                },
        };

        var b = new ClusterConfig()
        {
            ClusterId = "cluster1",
            Extensions = new Dictionary<Type, IConfigExtension>
            {
                { typeof(UserModel), new UserModel { UserName = "Super admin" } }
            }
        };

        var aExtension = a.GetExtension<UserModel>();
        var bExtension = b.GetExtension<UserModel>();

        Assert.False(a.Equals(b));
        Assert.False(aExtension.Equals(bExtension));
    }
}

public class UserModel : IConfigExtension
{
    public string UserName { get; set; }

    public override bool Equals(object obj)
    {
        return obj is UserModel other && Equals(other);
    }

    public bool Equals(UserModel other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(UserName, other.UserName, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserName?.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}
