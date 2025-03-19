// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class PathTransformExtensionsTests : TransformExtensionsTestsBase
{
    private readonly PathTransformFactory _factory;

    public PathTransformExtensionsTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddRouting();
        var services = serviceCollection.BuildServiceProvider();

        _factory = new PathTransformFactory(services.GetRequiredService<TemplateBinderFactory>());
    }

    [Fact]
    public void WithTransformPathSet()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformPathSet(new PathString("/path#"));

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidatePathSet(builderContext);
    }

    [Fact]
    public void AddPathSet()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddPathSet(new PathString("/path#"));

        ValidatePathSet(builderContext);
    }

    private static void ValidatePathSet(TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
        Assert.Equal(PathStringTransform.PathTransformMode.Set, pathStringTransform.Mode);
        Assert.Equal("/path#", pathStringTransform.Value.Value);
    }

    [Fact]
    public void WithTransformPathRemovePrefix()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformPathRemovePrefix(new PathString("/path#"));

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidatePathRemovePrefix(builderContext);
    }

    [Fact]
    public void AddPathRemovePrefix()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddPathRemovePrefix(new PathString("/path#"));

        ValidatePathRemovePrefix(builderContext);
    }

    private static void ValidatePathRemovePrefix(TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
        Assert.Equal(PathStringTransform.PathTransformMode.RemovePrefix, pathStringTransform.Mode);
        Assert.Equal("/path#", pathStringTransform.Value.Value);
    }

    [Fact]
    public void WithTransformPathPrefix()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformPathPrefix(new PathString("/path#"));

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidatePathPrefix(builderContext);
    }

    [Fact]
    public void AddPathPrefix()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddPathPrefix(new PathString("/path#"));

        ValidatePathPrefix(builderContext);
    }

    private static void ValidatePathPrefix(TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var pathStringTransform = Assert.IsType<PathStringTransform>(requestTransform);
        Assert.Equal(PathStringTransform.PathTransformMode.Prefix, pathStringTransform.Mode);
        Assert.Equal("/path#", pathStringTransform.Value.Value);
    }

    [Fact]
    public void WithTransformPathRouteValues()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformPathRouteValues(new PathString("/path#"));

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        ValidatePathRouteValues(builderContext);
    }

    [Fact]
    public void AddPathRouteValues()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddRouting();
        var services = serviceCollection.BuildServiceProvider();

        var builderContext = CreateBuilderContext(services: services);
        builderContext.AddPathRouteValues(new PathString("/path#"));

        ValidatePathRouteValues(builderContext);
    }

    private static void ValidatePathRouteValues(TransformBuilderContext builderContext)
    {
        var requestTransform = Assert.Single(builderContext.RequestTransforms);
        var pathRouteValuesTransform = Assert.IsType<PathRouteValuesTransform>(requestTransform);
        Assert.Equal("/path#", pathRouteValuesTransform.Pattern.RawText);
    }
}
