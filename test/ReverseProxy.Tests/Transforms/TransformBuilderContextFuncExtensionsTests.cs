// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class TransformBuilderContextFuncExtensionsTests : TransformExtentionsTestsBase
{
    [Fact]
    public void AddRequestTransform()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddRequestTransform(context =>
        {
            return default;
        });

        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        Assert.IsType<RequestFuncTransform>(requestTransform);
    }

    [Fact]
    public void AddResponseTransform()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddResponseTransform(context =>
        {
            return default;
        });

        var responseTransform = Assert.Single(builderContext.ResponseTransforms);
        Assert.IsType<ResponseFuncTransform>(responseTransform);
    }

    [Fact]
    public void AddResponseTrailersTransform()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddResponseTrailersTransform(context =>
        {
            return default;
        });

        var responseTrailersTransform = Assert.Single(builderContext.ResponseTrailersTransforms);
        Assert.IsType<ResponseTrailersFuncTransform>(responseTrailersTransform);
    }
}
