// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Delegation;

public class HttpSysDelegatorMiddlewareTests : TestAutoMockBase
{
    private readonly HttpSysDelegatorMiddleware _sut;
    private readonly RequestDelegate _next;
    private readonly DefaultHttpContext _context;
    private readonly ReverseProxyFeature _proxyFeature;
    private readonly List<DestinationState> _availableDestinations;

    private Action _nextCallback;
    private bool _nextCalled;

    public HttpSysDelegatorMiddlewareTests()
    {
        _context = new DefaultHttpContext();
        _availableDestinations = new List<DestinationState>();
        _proxyFeature = new ReverseProxyFeature
        {
            AvailableDestinations = _availableDestinations,
            Cluster = new ClusterModel(new ClusterConfig(), new HttpMessageInvoker(Mock<HttpMessageHandler>().Object)),
        };
        _context.Features.Set<IReverseProxyFeature>(_proxyFeature);
        _context.Features.Set(Mock<IHttpSysRequestDelegationFeature>().Object);

        Mock<IHttpSysRequestDelegationFeature>()
            .SetupGet(p => p.CanDelegate)
            .Returns(true);

        _next = context =>
        {
            _nextCalled = true;
            _nextCallback?.Invoke();
            return Task.CompletedTask;
        };
        Provide(_next);

        _sut = Create<HttpSysDelegatorMiddleware>();
    }

    [Fact]
    public async Task SingleDelegationDestination_VerifyProxiedDestinationSetAndNextNotCalled()
    {
        var destination = CreateDestination("dest1", "queue1");
        _availableDestinations.Add(destination);

        await _sut.Invoke(_context);

        Assert.Same(destination, _proxyFeature.ProxiedDestination);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task NoDestinations_VerifyNextInvoked()
    {
        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task NoDelegationDestinations_VerifyNextInvoked()
    {
        _availableDestinations.Add(CreateDestination("dest1", queueName: null));

        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task MultipleDestinations_OneDelegationAndOneProxyDestination_ProxyChoosen_VerifyNextInvokedWithSingleProxyDestination()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", queueName: null);
        _availableDestinations.Add(destination1);
        _availableDestinations.Add(destination2);
        SetupRandomToReturn(1); // return "dest2"

        _nextCallback = () =>
        {
            Assert.Single(_proxyFeature.AvailableDestinations);
            Assert.Same(destination2, _proxyFeature.AvailableDestinations[0]);
        };

        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task MultipleDestinations_OneDelegationAndOneProxyDestination_DelegationChoosen_VerifyProxiedDestinationSetAndNextNotCalled()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", queueName: null);
        _availableDestinations.Add(destination1);
        _availableDestinations.Add(destination2);
        SetupRandomToReturn(0); // return "dest1"

        await _sut.Invoke(_context);

        Assert.Same(destination1, _proxyFeature.ProxiedDestination);
        Assert.False(_nextCalled);
    }

    private static DestinationState CreateDestination(string id, string queueName = null)
    {
        var metadata = new Dictionary<string, string>();
        if (queueName != null)
        {
            metadata.Add(DelegationExtensions.HttpSysDelegationQueueMetadataKey, queueName);
        }

        var config = new DestinationConfig()
        {
            Address = "http://*:80",
            Metadata = metadata,
        };

        return new DestinationState(id)
        {
            Model = new DestinationModel(config),
        };
    }

    private void SetupRandomToReturn(int value)
    {
        Mock<IRandomFactory>().Setup(m => m.CreateRandomInstance()).Returns(new TestRandom(value));
    }

    private class TestRandom : Random
    {
        private readonly int _next;

        public TestRandom(int next)
        {
            _next = next;
        }

        public override int Next(int maxValue)
        {
            return _next;
        }
    }
}
