// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class QueryTransformExtensionsTests : TransformExtensionsTestsBase
{
    private readonly QueryTransformFactory _factory = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithTransformQueryRouteValue(bool append)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformQueryRouteValue("key", "value", append);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidateQueryRouteParameter(append, builderContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddQueryRouteValue(bool append)
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddQueryRouteValue("key", "value", append);

        ValidateQueryRouteParameter(append, builderContext);
    }

    private static void ValidateQueryRouteParameter(bool append, TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var queryParameterRouteTransform = Assert.IsType<QueryParameterRouteTransform>(requestTransform);
        Assert.Equal("key", queryParameterRouteTransform.Key);
        Assert.Equal("value", queryParameterRouteTransform.RouteValueKey);
        var expectedMode = append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set;
        Assert.Equal(expectedMode, queryParameterRouteTransform.Mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithTransformQueryValue(bool append)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformQueryValue("key", "value", append);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidateQueryValue(append, builderContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddQueryValue(bool append)
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddQueryValue("key", "value", append);

        ValidateQueryValue(append, builderContext);
    }

    private static void ValidateQueryValue(bool append, TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var queryParameterFromStaticTransform = Assert.IsType<QueryParameterFromStaticTransform>(requestTransform);
        Assert.Equal("key", queryParameterFromStaticTransform.Key);
        Assert.Equal("value", queryParameterFromStaticTransform.Value);
        var expectedMode = append ? QueryStringTransformMode.Append : QueryStringTransformMode.Set;
        Assert.Equal(expectedMode, queryParameterFromStaticTransform.Mode);
    }

    [Fact]
    public void WithTransformQueryRemoveKey()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformQueryRemoveKey("key");

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidateQueryRemoveKey(builderContext);
    }

    [Fact]
    public void AddQueryRemoveKey()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddQueryRemoveKey("key");

        ValidateQueryRemoveKey(builderContext);
    }

    private static void ValidateQueryRemoveKey(TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var removeQueryParameterTransform = Assert.IsType<QueryParameterRemoveTransform>(requestTransform);
        Assert.Equal("key", removeQueryParameterTransform.Key);
    }
}
