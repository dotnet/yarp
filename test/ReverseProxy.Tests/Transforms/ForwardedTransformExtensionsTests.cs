// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms.Tests;

public class ForwardedTransformExtensionsTests : TransformExtensionsTestsBase
{
    private readonly ForwardedTransformFactory _factory = new ForwardedTransformFactory(new TestRandomFactory());

    [Theory]
    [InlineData(ForwardedTransformActions.Set, null, null, null, null)]
    [InlineData(ForwardedTransformActions.Append, ForwardedTransformActions.Set, null, null, null)]
    [InlineData(ForwardedTransformActions.Append, null, ForwardedTransformActions.Set, null, null)]
    [InlineData(ForwardedTransformActions.Append, null, null, ForwardedTransformActions.Set, null)]
    [InlineData(ForwardedTransformActions.Append, null, null, null, ForwardedTransformActions.Set)]
    [InlineData(ForwardedTransformActions.Append, ForwardedTransformActions.Off, null, null, null)]
    [InlineData(ForwardedTransformActions.Append, null, ForwardedTransformActions.Off, null, null)]
    [InlineData(ForwardedTransformActions.Append, null, null, ForwardedTransformActions.Off, null)]
    [InlineData(ForwardedTransformActions.Append, null, null, null, ForwardedTransformActions.Off)]
    [InlineData(ForwardedTransformActions.Set, ForwardedTransformActions.Append, ForwardedTransformActions.Remove, ForwardedTransformActions.Off, ForwardedTransformActions.Remove)]
    public void WithTransformXForwarded(
        ForwardedTransformActions xDefault,
        ForwardedTransformActions? xFor,
        ForwardedTransformActions? xHost,
        ForwardedTransformActions? xProto,
        ForwardedTransformActions? xPrefix)
    {
        var routeConfig = new RouteConfig();
        var prefix = "prefix-";
        routeConfig = routeConfig.WithTransformXForwarded(prefix, xDefault, xFor, xHost, xProto, xPrefix);

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        if (xFor != ForwardedTransformActions.Off)
        {
            ValidateXForwardedTransform("For", prefix, xFor ?? xDefault, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>().Single());
        }
        else
        {
            Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedForTransform>());
        }

        if (xHost != ForwardedTransformActions.Off)
        {
            ValidateXForwardedTransform("Host", prefix, xHost ?? xDefault, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>().Single());
        }
        else
        {
            Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedHostTransform>());
        }

        if (xProto != ForwardedTransformActions.Off)
        {
            ValidateXForwardedTransform("Proto", prefix, xProto ?? xDefault, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>().Single());
        }
        else
        {
            Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedProtoTransform>());
        }

        if (xPrefix != ForwardedTransformActions.Off)
        {
            ValidateXForwardedTransform("Prefix", prefix, xPrefix ?? xDefault, builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPrefixTransform>().Single());
        }
        else
        {
            Assert.Empty(builderContext.RequestTransforms.OfType<RequestHeaderXForwardedPrefixTransform>());
        }
    }

    [Theory]
    [MemberData(nameof(GetAddXForwardedCases))]
    public void AddXForwarded(Func<TransformBuilderContext, string, ForwardedTransformActions, TransformBuilderContext> addFunc,
        string transformName, ForwardedTransformActions action)
    {
        var builderContext = CreateBuilderContext();
        addFunc(builderContext, "prefix-" + transformName, action);

        ValidateXForwarded(builderContext, transformName, "prefix-", action);
    }

    public static IEnumerable<object[]> GetAddXForwardedCases()
    {
        var actions = (ForwardedTransformActions[])Enum.GetValues(typeof(ForwardedTransformActions));
        var addTransformFuncs = new (Func<TransformBuilderContext, string, ForwardedTransformActions, TransformBuilderContext>, string)[]
        {
            (ForwardedTransformExtensions.AddXForwardedFor, "For"), (ForwardedTransformExtensions.AddXForwardedPrefix, "Prefix"),
            (ForwardedTransformExtensions.AddXForwardedHost, "Host"), (ForwardedTransformExtensions.AddXForwardedProto, "Proto")
        };

        return addTransformFuncs.Join(actions, _ => true, _ => true, (t, a) => new object[] { t.Item1, t.Item2, a });
    }

    private static void ValidateXForwarded(TransformBuilderContext builderContext, string transformName, string headerPrefix, ForwardedTransformActions action)
    {
        Assert.False(builderContext.UseDefaultForwarders);

        if (action == ForwardedTransformActions.Off)
        {
            Assert.Empty(builderContext.RequestTransforms);
        }
        else
        {
            var transform = Assert.Single(builderContext.RequestTransforms);
            ValidateXForwardedTransform(transformName, headerPrefix, action, transform);
        }
    }

    private static void ValidateXForwardedTransform(string transformName, string headerPrefix, ForwardedTransformActions action, RequestTransform transform)
    {
        Assert.Equal($"RequestHeaderXForwarded{transformName}Transform", transform.GetType().Name);
        Assert.Equal(headerPrefix + transformName, ((dynamic)transform).HeaderName);
        Assert.Equal(action, ((dynamic)transform).TransformAction);
    }

    [Theory]
    [InlineData(NodeFormat.Random, true, true, NodeFormat.Random, ForwardedTransformActions.Append)]
    [InlineData(NodeFormat.RandomAndPort, true, true, NodeFormat.Random, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Append)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Append)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.None, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.RandomAndPort, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Unknown, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.UnknownAndPort, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Ip, ForwardedTransformActions.Set)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.IpAndPort, ForwardedTransformActions.Set)]
    public void WithTransformForwarded(NodeFormat forFormat, bool useHost, bool useProto, NodeFormat byFormat, ForwardedTransformActions action)
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformForwarded(useHost, useProto, forFormat, byFormat, action);

        var builderContext = ValidateAndBuild(routeConfig, _factory, CreateServices());

        ValidateForwarded(builderContext, useHost, useProto, forFormat, byFormat, action, true);
    }

    [Theory]
    [InlineData(NodeFormat.Random, true, true, NodeFormat.Random, ForwardedTransformActions.Append, true)]
    [InlineData(NodeFormat.Random, true, true, NodeFormat.Random, ForwardedTransformActions.Append, false)]
    [InlineData(NodeFormat.RandomAndPort, true, true, NodeFormat.Random, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.RandomAndPort, true, true, NodeFormat.Random, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Append, true)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Append, false)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, false, NodeFormat.None, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Append, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Append, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Random, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.None, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.None, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.RandomAndPort, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.RandomAndPort, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Unknown, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Unknown, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.UnknownAndPort, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.UnknownAndPort, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Ip, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.Ip, ForwardedTransformActions.Set, false)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.IpAndPort, ForwardedTransformActions.Set, true)]
    [InlineData(NodeFormat.None, false, true, NodeFormat.IpAndPort, ForwardedTransformActions.Set, false)]
    public void AddForwarded(NodeFormat forFormat, bool useHost, bool useProto, NodeFormat byFormat, ForwardedTransformActions action, bool removeAllXForwardedHeaders)
    {
        var builderContext = CreateBuilderContext(services: CreateServices());
        builderContext.AddForwarded(useHost, useProto, forFormat, byFormat, action, removeAllXForwardedHeaders);

        ValidateForwarded(builderContext, useHost, useProto, forFormat, byFormat, action, removeAllXForwardedHeaders);
    }

    private static void ValidateForwarded(TransformBuilderContext builderContext, bool useHost, bool useProto,
        NodeFormat forFormat, NodeFormat byFormat, ForwardedTransformActions action, bool removeAllXForwardedHeaders)
    {
        Assert.False(builderContext.UseDefaultForwarders);

        if (byFormat != NodeFormat.None || forFormat != NodeFormat.None || useHost || useProto)
        {
            if (removeAllXForwardedHeaders)
            {
                Assert.Equal(5, builderContext.RequestTransforms.Count);
                Assert.All(
                    builderContext.RequestTransforms.Skip(1).Select(t => (dynamic)t),
                    t =>
                    {
                        Assert.StartsWith("X-Forwarded-", t.HeaderName);
                        Assert.Equal(ForwardedTransformActions.Remove, t.TransformAction);
                    });
            }
            else
            {
                Assert.Equal(1, builderContext.RequestTransforms.Count);
                var xForwardedTransforms = builderContext.RequestTransforms.Skip(1).Cast<dynamic>().Where(requestTransform =>
                    requestTransform.HeaderName.ToLowerInvariant().StartsWith("x-forwarded")).ToList();
                Assert.Empty(xForwardedTransforms);
            }

            var transform = builderContext.RequestTransforms[0];
            var requestHeaderForwardedTransform = Assert.IsType<RequestHeaderForwardedTransform>(transform);
            Assert.Equal(action, requestHeaderForwardedTransform.TransformAction);
            Assert.Equal(useHost, requestHeaderForwardedTransform.HostEnabled);
            Assert.Equal(useProto, requestHeaderForwardedTransform.ProtoEnabled);
            Assert.Equal(byFormat, requestHeaderForwardedTransform.ByFormat);
            Assert.Equal(forFormat, requestHeaderForwardedTransform.ForFormat);
        }
        else
        {
            Assert.Empty(builderContext.RequestTransforms);
        }
    }

    [Fact]
    public void WithTransformClientCertHeader()
    {
        var routeConfig = new RouteConfig();
        routeConfig = routeConfig.WithTransformClientCertHeader("name");

        var builderContext = ValidateAndBuild(routeConfig, _factory);

        var transform = Assert.Single(builderContext.RequestTransforms);
        var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
        Assert.Equal("name", certTransform.HeaderName);
    }

    [Fact]
    public void AddClientCertHeader()
    {
        var builderContext = CreateBuilderContext();
        builderContext.AddClientCertHeader("name");

        var transform = Assert.Single(builderContext.RequestTransforms);
        var certTransform = Assert.IsType<RequestHeaderClientCertTransform>(transform);
        Assert.Equal("name", certTransform.HeaderName);
    }

    private static IServiceProvider CreateServices()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IRandomFactory, TestRandomFactory>();
        return collection.BuildServiceProvider();
    }
}
