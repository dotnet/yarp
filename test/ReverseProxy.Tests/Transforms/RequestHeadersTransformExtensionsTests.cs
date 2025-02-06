// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class RequestHeadersTransformExtensionsTests : TransformExtentionsTestsBase
{
    private readonly RequestHeadersTransformFactory _factory = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithTransformCopyRequestHeaders(bool copy)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformCopyRequestHeaders(copy);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        Assert.Equal(copy, builderContext.CopyRequestHeaders);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithTransformUseOriginalHostHeader(bool useOriginal)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformUseOriginalHostHeader(useOriginal);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        var transform = Assert.Single(builderContext.RequestTransforms);
        var hostTransform = Assert.IsType<RequestHeaderOriginalHostTransform>(transform);

        Assert.Equal(useOriginal, hostTransform.UseOriginalHost);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithTransformRequestHeader(bool append)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformRequestHeader("name", "value", append);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidateRequestHeader(append, builderContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithTransformRequestHeaderRouteValue(bool append)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformRequestHeaderRouteValue("key", "value", append);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidateHeaderRouteParameter(append, builderContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddRequestHeader(bool append)
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddRequestHeader("name", "value", append);

        ValidateRequestHeader(append, builderContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddRequestHeaderRouteValue(bool append)
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddRequestHeaderRouteValue("key", "value", append);

        ValidateHeaderRouteParameter(append, builderContext);
    }

    [Fact]
    public void WithTransformRequestHeaderRemove()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformRequestHeaderRemove("MyHeader");

        var builderContext = ValidateAndBuild(routeConfig, _factory);
        var transform = Assert.Single(builderContext.RequestTransforms) as RequestHeaderRemoveTransform;
        Assert.Equal("MyHeader", transform.HeaderName);
    }

    [Fact]
    public void AddRequestHeaderRemove()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddRequestHeaderRemove("MyHeader");

        var transform = Assert.Single(builderContext.RequestTransforms) as RequestHeaderRemoveTransform;
        Assert.Equal("MyHeader", transform.HeaderName);
    }

    [Fact]
    public void WithTransformRequestHeadersAllowed()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformRequestHeadersAllowed("header1", "Header2");

        var builderContext = ValidateAndBuild(routeConfig, _factory);
        var transform = Assert.Single(builderContext.RequestTransforms) as RequestHeadersAllowedTransform;
        Assert.Equal(new[] { "header1", "Header2" }, transform.AllowedHeaders);
        Assert.False(builderContext.CopyRequestHeaders);
    }

    [Fact]
    public void AddRequestHeadersAllowed()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddRequestHeadersAllowed("header1", "Header2");

        var transform = Assert.Single(builderContext.RequestTransforms) as RequestHeadersAllowedTransform;
        Assert.Equal(new[] { "header1", "Header2" }, transform.AllowedHeaders);
        Assert.False(builderContext.CopyRequestHeaders);
    }

    private static void ValidateRequestHeader(bool append, TransformBuilderContext builderContext)
    {
        var requestHeaderValueTransform = Assert.Single(builderContext.RequestTransforms.OfType<RequestHeaderValueTransform>(), t => t.HeaderName == "name");
        Assert.Equal("value", requestHeaderValueTransform.Value);
        Assert.Equal(append, requestHeaderValueTransform.Append);
    }

    private static void ValidateHeaderRouteParameter(bool append, TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var requestHeaderFromRouteTransform = Assert.IsType<RequestHeaderRouteValueTransform>(requestTransform);
        Assert.Equal("key", requestHeaderFromRouteTransform.HeaderName);
        Assert.Equal("value", requestHeaderFromRouteTransform.RouteValueKey);
        var expectedMode = append;
        Assert.Equal(expectedMode, requestHeaderFromRouteTransform.Append);
    }
}
