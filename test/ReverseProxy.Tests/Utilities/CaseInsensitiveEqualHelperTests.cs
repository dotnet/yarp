// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Yarp.ReverseProxy.Utilities.Tests;

public class CaseInsensitiveEqualHelperTests
{
    [Fact]
    public void Equals_Same_Instance_Returns_True()
    {
        var list1 = new string[] { "item1", "item2" };

        var equals = CaseInsensitiveEqualHelper.Equals(list1, list1);

        Assert.True(equals);
    }

    [Fact]
    public void Equals_Empty_List_Returns_True()
    {
        var list1 = System.Array.Empty<string>();

        var list2 = System.Array.Empty<string>();

        var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

        Assert.True(equals);
    }

    [Fact]
    public void Equals_List_Same_Value_Returns_True()
    {
        var list1 = new string[] { "item1", "item2" };

        var list2 = new string[] { "item1", "item2" };

        var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

        Assert.True(equals);
    }

    [Fact]
    public void Equals_List_Different_Value_Returns_False()
    {
        var list1 = new string[] { "item1", "item2" };

        var list2 = new string[] { "item3", "item4" };

        var equals = CaseInsensitiveEqualHelper.Equals(list1, list2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_First_List_Null_Returns_False()
    {
        var list2 = new string[] { "item1", "item2" };

        var equals = CaseInsensitiveEqualHelper.Equals(null, list2);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Second_List_Null_Returns_False()
    {
        var list1 = new string[] { "item1", "item2" };

        var equals = CaseInsensitiveEqualHelper.Equals(list1, null);

        Assert.False(equals);
    }

    [Fact]
    public void Equals_Null_List_Returns_True()
    {
        var equals = CaseInsensitiveEqualHelper.Equals(list1: null, list2: null);

        Assert.True(equals);
    }
}
