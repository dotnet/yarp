// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity.Tests;

public class SessionAffinityDifferentialTests
{
    [Theory]
    [InlineData("HTTP/1.1", false, false)]
    [InlineData("HTTP/1.1", true, true)]
    [InlineData("HTTP/2", false, true)]
    [InlineData("HTTP/2", true, false)]
    public async Task Invoke_PreservesProviderNextOrderingAndReassignment(string protocol, bool asyncProvider, bool asyncNext)
    {
        var events = new List<string>();
        var (cluster, destinations, context) = CreateContext(protocol);
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.OK, destinations[1], asyncProvider, events);
        var middleware = new SessionAffinityMiddleware(async httpContext =>
        {
            events.Add("next-start");
            Assert.Same(destinations[1], httpContext.GetReverseProxyFeature().AvailableDestinations.Single());
            if (asyncNext)
            {
                await Task.Yield();
            }
            events.Add("next-end");
        }, new[] { policy }, Array.Empty<IAffinityFailurePolicy>(), NullLogger<SessionAffinityMiddleware>.Instance);

        await middleware.Invoke(context);

        Assert.Equal(new[] { "provider-start", "provider-end", "next-start", "next-end" }, events);
        Assert.Same(cluster.Model, context.GetReverseProxyFeature().Cluster);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public async Task Invoke_PreservesFailurePolicyOrdering(bool asyncProvider, bool asyncFailurePolicy, bool keepProcessing)
    {
        var events = new List<string>();
        var (_, _, context) = CreateContext("HTTP/1.1");
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.DestinationNotFound, null, asyncProvider, events);
        var failurePolicy = new TestFailurePolicy("CustomFailure", keepProcessing, asyncFailurePolicy, events);
        var middleware = new SessionAffinityMiddleware(httpContext =>
        {
            events.Add("next");
            return Task.CompletedTask;
        }, new[] { policy }, new[] { failurePolicy }, NullLogger<SessionAffinityMiddleware>.Instance);

        await middleware.Invoke(context);

        var expected = keepProcessing
            ? new[] { "provider-start", "provider-end", "failure-start", "failure-end", "next" }
            : new[] { "provider-start", "provider-end", "failure-start", "failure-end" };
        Assert.Equal(expected, events);
        Assert.Equal(keepProcessing ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_ConcurrentRequestsKeepDestinationStateIsolated()
    {
        var observed = new ConcurrentBag<DestinationState>();
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.OK, destination: null, isAsync: false, events: null);
        var middleware = new SessionAffinityMiddleware(context =>
        {
            observed.Add(context.GetReverseProxyFeature().AvailableDestinations.Single());
            return Task.CompletedTask;
        }, new[] { policy }, Array.Empty<IAffinityFailurePolicy>(), NullLogger<SessionAffinityMiddleware>.Instance);

        var contexts = Enumerable.Range(0, 256).Select(index =>
        {
            var (_, destinations, context) = CreateContext(index % 2 == 0 ? "HTTP/1.1" : "HTTP/2");
            policy.SetDestination(context, destinations[index % destinations.Count]);
            return context;
        }).ToArray();

        await Task.WhenAll(contexts.Select(context => Task.Run(() => middleware.Invoke(context))));

        Assert.Equal(contexts.Length, observed.Count);
        Assert.All(contexts, context =>
        {
            var expected = policy.GetDestination(context);
            Assert.Same(expected, context.GetReverseProxyFeature().AvailableDestinations.Single());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invoke_ProviderExceptionsAreCapturedInReturnedTask(bool isAsync)
    {
        var expected = new InvalidOperationException("provider failure");
        var (_, _, context) = CreateContext("HTTP/1.1");
        var middleware = new SessionAffinityMiddleware(
            _ => Task.CompletedTask,
            new ISessionAffinityPolicy[] { new ThrowingAffinityPolicy(expected, isAsync) },
            Array.Empty<IAffinityFailurePolicy>(),
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => invokeTask);
        Assert.Same(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invoke_FailurePolicyExceptionsAreCapturedInReturnedTask(bool isAsync)
    {
        var expected = new InvalidOperationException("failure policy failure");
        var (_, _, context) = CreateContext("HTTP/1.1");
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.DestinationNotFound, null, isAsync: false, events: null);
        var middleware = new SessionAffinityMiddleware(
            _ => Task.CompletedTask,
            new[] { policy },
            new IAffinityFailurePolicy[] { new ThrowingFailurePolicy(expected, isAsync) },
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => invokeTask);
        Assert.Same(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invoke_NextExceptionsAreCapturedInReturnedTask(bool isAsync)
    {
        var expected = new InvalidOperationException("next failure");
        var (_, destinations, context) = CreateContext("HTTP/1.1");
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.OK, destinations[0], isAsync: false, events: null);
        RequestDelegate next = isAsync
            ? async _ =>
            {
                await Task.Yield();
                throw expected;
            }
            : _ => throw expected;
        var middleware = new SessionAffinityMiddleware(
            next,
            new[] { policy },
            Array.Empty<IAffinityFailurePolicy>(),
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => invokeTask);
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Invoke_CanceledRequestProducesCanceledTask()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var (_, destinations, context) = CreateContext("HTTP/1.1");
        context.RequestAborted = cancellation.Token;
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.OK, destinations[0], isAsync: false, events: null);
        var middleware = new SessionAffinityMiddleware(
            _ => Task.CompletedTask,
            new[] { policy },
            Array.Empty<IAffinityFailurePolicy>(),
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        Assert.True(invokeTask.IsCanceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
    }

    [Fact]
    public async Task Invoke_AsyncCanceledProviderProducesCanceledTask()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var (_, _, context) = CreateContext("HTTP/1.1");
        var middleware = new SessionAffinityMiddleware(
            _ => Task.CompletedTask,
            new ISessionAffinityPolicy[] { new AsyncCanceledAffinityPolicy() },
            Array.Empty<IAffinityFailurePolicy>(),
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        Assert.True(invokeTask.IsCanceled);
    }

    [Fact]
    public async Task Invoke_NullTaskFromNextIsCapturedInReturnedTask()
    {
        var (_, destinations, context) = CreateContext("HTTP/1.1");
        var policy = new TestAffinityPolicy("Custom", AffinityStatus.OK, destinations[0], isAsync: false, events: null);
        var middleware = new SessionAffinityMiddleware(
            _ => null!,
            new[] { policy },
            Array.Empty<IAffinityFailurePolicy>(),
            NullLogger<SessionAffinityMiddleware>.Instance);

        var invokeTask = middleware.Invoke(context);

        await Assert.ThrowsAsync<NullReferenceException>(() => invokeTask);
    }

    private static (ClusterState Cluster, IReadOnlyList<DestinationState> Destinations, DefaultHttpContext Context) CreateContext(string protocol)
    {
        var cluster = new ClusterState("cluster");
        var destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };
        var config = new ClusterConfig
        {
            ClusterId = cluster.ClusterId,
            SessionAffinity = new SessionAffinityConfig
            {
                Enabled = true,
                Policy = "Custom",
                FailurePolicy = "CustomFailure",
                AffinityKeyName = "Affinity",
            },
        };
        cluster.Model = new ClusterModel(config, new HttpMessageInvoker(new HttpClientHandler()));
        cluster.DestinationsState = new ClusterDestinationsState(destinations, destinations);

        var context = new DefaultHttpContext();
        context.Request.Protocol = protocol;
        context.Features.Set<IReverseProxyFeature>(new ReverseProxyFeature
        {
            Route = new RouteModel(new RouteConfig(), cluster, HttpTransformer.Default),
            Cluster = cluster.Model,
            AllDestinations = destinations,
            AvailableDestinations = destinations,
        });
        return (cluster, destinations, context);
    }

    private sealed class TestAffinityPolicy : ISessionAffinityPolicy
    {
        private readonly AffinityStatus _status;
        private readonly DestinationState _destination;
        private readonly bool _isAsync;
        private readonly List<string> _events;
        private readonly ConcurrentDictionary<HttpContext, DestinationState> _destinations = new();

        public TestAffinityPolicy(string name, AffinityStatus status, DestinationState destination, bool isAsync, List<string> events)
        {
            Name = name;
            _status = status;
            _destination = destination;
            _isAsync = isAsync;
            _events = events;
        }

        public string Name { get; }

        public AffinityResult FindAffinitizedDestinations(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations)
        {
            return Complete(context);
        }

        public ValueTask<AffinityResult> FindAffinitizedDestinationsAsync(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events?.Add("provider-start");
            return _isAsync ? CompleteAsync(context) : new ValueTask<AffinityResult>(CompleteAfterStart(context));
        }

        public void AffinitizeResponse(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            DestinationState destination)
        {
        }

        public void SetDestination(HttpContext context, DestinationState destination) => _destinations[context] = destination;

        public DestinationState GetDestination(HttpContext context) => _destinations[context];

        private async ValueTask<AffinityResult> CompleteAsync(HttpContext context)
        {
            await Task.Yield();
            return CompleteAfterStart(context);
        }

        private AffinityResult Complete(HttpContext context)
        {
            _events?.Add("provider-start");
            return CompleteAfterStart(context);
        }

        private AffinityResult CompleteAfterStart(HttpContext context)
        {
            _events?.Add("provider-end");
            var destination = _destination ?? _destinations.GetValueOrDefault(context);
            return new AffinityResult(destination, _status);
        }
    }

    private sealed class TestFailurePolicy : IAffinityFailurePolicy
    {
        private readonly bool _keepProcessing;
        private readonly bool _isAsync;
        private readonly List<string> _events;

        public TestFailurePolicy(string name, bool keepProcessing, bool isAsync, List<string> events)
        {
            Name = name;
            _keepProcessing = keepProcessing;
            _isAsync = isAsync;
            _events = events;
        }

        public string Name { get; }

        public Task<bool> Handle(HttpContext context, ClusterState cluster, AffinityStatus affinityStatus)
        {
            _events.Add("failure-start");
            if (!_keepProcessing)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            return _isAsync ? CompleteAsync() : Task.FromResult(Complete());
        }

        private async Task<bool> CompleteAsync()
        {
            await Task.Yield();
            return Complete();
        }

        private bool Complete()
        {
            _events.Add("failure-end");
            return _keepProcessing;
        }
    }

    private sealed class ThrowingAffinityPolicy : ISessionAffinityPolicy
    {
        private readonly Exception _exception;
        private readonly bool _isAsync;

        public ThrowingAffinityPolicy(Exception exception, bool isAsync)
        {
            _exception = exception;
            _isAsync = isAsync;
        }

        public string Name => "Custom";

        public AffinityResult FindAffinitizedDestinations(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations)
        {
            throw _exception;
        }

        public ValueTask<AffinityResult> FindAffinitizedDestinationsAsync(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations,
            CancellationToken cancellationToken)
        {
            return _isAsync ? ThrowAsync() : throw _exception;
        }

        public void AffinitizeResponse(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            DestinationState destination)
        {
        }

        private async ValueTask<AffinityResult> ThrowAsync()
        {
            await Task.Yield();
            throw _exception;
        }
    }

    private sealed class AsyncCanceledAffinityPolicy : ISessionAffinityPolicy
    {
        public string Name => "Custom";

        public AffinityResult FindAffinitizedDestinations(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations)
        {
            throw new NotSupportedException();
        }

        public async ValueTask<AffinityResult> FindAffinitizedDestinationsAsync(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            IReadOnlyList<DestinationState> destinations,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new OperationCanceledException(cancellationToken);
        }

        public void AffinitizeResponse(
            HttpContext context,
            ClusterState cluster,
            SessionAffinityConfig config,
            DestinationState destination)
        {
        }
    }

    private sealed class ThrowingFailurePolicy : IAffinityFailurePolicy
    {
        private readonly Exception _exception;
        private readonly bool _isAsync;

        public ThrowingFailurePolicy(Exception exception, bool isAsync)
        {
            _exception = exception;
            _isAsync = isAsync;
        }

        public string Name => "CustomFailure";

        public Task<bool> Handle(HttpContext context, ClusterState cluster, AffinityStatus affinityStatus)
        {
            return _isAsync ? ThrowAsync() : throw _exception;
        }

        private async Task<bool> ThrowAsync()
        {
            await Task.Yield();
            throw _exception;
        }
    }
}
