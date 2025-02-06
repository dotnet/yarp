// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class PathStringTransformTests
{
    [Theory]
    [InlineData("/foo", "Set", "/value", "/value")]
    [InlineData("/foo", "Set", "", "")]
    [InlineData("/foo", "Prefix", "/value", "/value/foo")]
    [InlineData("/value/foo", "RemovePrefix", "/value", "/foo")]
    public async Task Set_Path_Success(string initialValue, string modeString, string transformValue, string expected)
    {
        // We can't put an internal type in a public test API parameter.
        var mode = Enum.Parse<PathStringTransform.PathTransformMode>(modeString);
        var context = new RequestTransformContext() { Path = initialValue };
        var transform = new PathStringTransform(mode, transformValue);
        await transform.ApplyAsync(context);
        Assert.Equal(expected, context.Path);
    }
}
