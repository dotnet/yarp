// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Yarp.Kubernetes.Controller.Hosting;
using Yarp.Kubernetes.Controller.Rate;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Class ResourceInformer.
/// Implements the <see cref="IResourceInformer{TResource}" />.
/// Implements the <see cref="IDisposable" />.
/// </summary>
/// <typeparam name="TResource">The type of the t resource.</typeparam>
/// <typeparam name="TListResource">The type of the t resource used in lists.</typeparam>
/// <seealso cref="IResourceInformer{TResource}" />
/// <seealso cref="IDisposable" />
public abstract class ResourceInformer<TResource, TListResource> : BackgroundHostedService, IResourceInformer<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    where TListResource : class, IKubernetesObject<V1ListMeta>, IItems<TResource>, new()
{
    private readonly object _sync = new object();
    private readonly GroupApiVersionKind _names;
    private readonly SemaphoreSlim _ready = new SemaphoreSlim(0);
    private readonly SemaphoreSlim _start = new SemaphoreSlim(0);
    private readonly ResourceSelector<TResource> _selector;
    private ImmutableList<Registration> _registrations = ImmutableList<Registration>.Empty;
    private Dictionary<NamespacedName, IList<V1OwnerReference>> _cache = [];
    private string _lastResourceVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceInformer{TResource, TListResource}" /> class.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="selector">A resource selector for (optionally) filtering the list of resources.</param>
    /// <param name="hostApplicationLifetime">The host application lifetime.</param>
    /// <param name="logger">The logger.</param>
    public ResourceInformer(
        IKubernetes client,
        ResourceSelector<TResource> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger logger)
        : base(hostApplicationLifetime, logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(selector);

        Client = client;
        _selector = selector;
        _names = GroupApiVersionKind.From<TResource>();
    }

    private enum EventType
    {
        SynchronizeStarted = 101,
        SynchronizeComplete = 102,
        WatchingResource = 103,
        ReceivedError = 104,
        WatchingComplete = 105,
        InformerWatchEvent = 106,
        DisposingToReconnect = 107,
        IgnoringError = 108,
    }

    protected IKubernetes Client { get; init; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _start.Dispose();
                _ready.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // ignore redundant exception to allow shutdown sequence to progress uninterrupted
            }
        }
        base.Dispose(disposing);
    }

    public void StartWatching()
    {
        _start.Release();
    }

    public virtual IResourceInformerRegistration Register(ResourceInformerCallback<TResource> callback)
    {
        return new Registration(this, callback);
    }

    public IResourceInformerRegistration Register(ResourceInformerCallback<IKubernetesObject<V1ObjectMeta>> callback)
    {
        return new Registration(this, (eventType, resource) => callback(eventType, resource));
    }

    /// <inheritdoc/>
    public virtual async Task ReadyAsync(CancellationToken cancellationToken)
    {
        await _ready.WaitAsync(cancellationToken).ConfigureAwait(false);

        // Release is called after each WaitAsync because
        // the semaphore is being used as a manual reset event
        _ready.Release();
    }

    /// <summary>
    /// RunAsync starts processing when StartAsync is called, and is terminated when
    /// StopAsync is called.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _start.WaitAsync(cancellationToken).ConfigureAwait(false);

            var limiter = new Limiter(new Limit(0.2), 3);
            var shouldSync = true;
            var firstSync = true;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (shouldSync)
                    {
                        await ListAsync(cancellationToken).ConfigureAwait(true);
                        shouldSync = false;
                    }

                    if (firstSync)
                    {
                        _ready.Release();
                        firstSync = false;
                    }

                    await WatchAsync(cancellationToken).ConfigureAwait(true);
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    Logger.LogDebug(
                        EventId(EventType.ReceivedError),
                        "Received error watching {ResourceType}: {ErrorMessage}",
                        typeof(TResource).Name,
                        ex.Message);
                }
                catch (KubernetesException ex)
                {
                    Logger.LogDebug(
                        EventId(EventType.ReceivedError),
                        "Received error watching {ResourceType}: {ErrorMessage}",
                        typeof(TResource).Name,
                        ex.Message);

                    // deal with this non-recoverable condition "too old resource version"
                    // with a re-sync to listing everything again ensuring no subscribers miss updates
                    if (ex is KubernetesException kubernetesError)
                    {
                        if (string.Equals(kubernetesError.Status.Reason, "Expired", StringComparison.Ordinal))
                        {
                            shouldSync = true;
                        }
                    }
                }

                // rate limiting the reconnect loop
                await limiter.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception error)
        {
            Logger.LogInformation(
                EventId(EventType.WatchingComplete),
                error,
                "No longer watching {ResourceType} resources from API server.",
                typeof(TResource).Name);
            throw;
        }
    }

    protected abstract Task<HttpOperationResponse<TListResource>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, ResourceSelector<TResource> resourceSelector = null, CancellationToken cancellationToken = default);

    private static EventId EventId(EventType eventType) => new EventId((int)eventType, eventType.ToString());

    private async Task ListAsync(CancellationToken cancellationToken)
    {
        var previousCache = _cache;
        _cache = new Dictionary<NamespacedName, IList<V1OwnerReference>>();

        if (_selector.FieldSelector is not null)
        {
            Logger.LogInformation(
                EventId(EventType.SynchronizeStarted),
                "Started synchronizing {ResourceType} resources from API server with field selector '{FieldSelector}'.",
                typeof(TResource).Name,
                _selector.FieldSelector);
        }
        else
        {
            Logger.LogInformation(
                EventId(EventType.SynchronizeStarted),
                "Started synchronizing {ResourceType} resources from API server.",
                typeof(TResource).Name);
        }

        string continueParameter = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            // request next page of items
            using var listWithHttpMessage = await RetrieveResourceListAsync(resourceVersion: _lastResourceVersion, resourceSelector: _selector, cancellationToken: cancellationToken);

            var list = listWithHttpMessage.Body;
            foreach (var item in list.Items)
            {
                // These properties are not already set on items while listing
                // assigned here for consistency
                item.ApiVersion = _names.GroupApiVersion;
                item.Kind = _names.Kind;

                var key = NamespacedName.From(item);
                _cache[key] = item?.Metadata?.OwnerReferences;

                var watchEventType = WatchEventType.Added;
                if (previousCache.Remove(key))
                {
                    // an already-known key is provided as a modification for re-sync purposes
                    watchEventType = WatchEventType.Modified;
                }

                InvokeRegistrationCallbacks(watchEventType, item);
            }

            foreach (var (key, value) in previousCache)
            {
                // for anything which was previously known but not part of list
                // send a deleted notification to clear any observer caches
                var item = new TResource()
                {
                    ApiVersion = _names.GroupApiVersion,
                    Kind = _names.Kind,
                    Metadata = new V1ObjectMeta(
                        name: key.Name,
                        namespaceProperty: key.Namespace,
                        ownerReferences: value),
                };

                InvokeRegistrationCallbacks(WatchEventType.Deleted, item);
            }

            // keep track of values needed for next page and to start watching
            _lastResourceVersion = list.ResourceVersion();
            continueParameter = list.Continue();
        }
        while (!string.IsNullOrEmpty(continueParameter));

        Logger.LogInformation(
            EventId(EventType.SynchronizeComplete),
            "Completed synchronizing {ResourceType} resources from API server.",
            typeof(TResource).Name);
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            EventId(EventType.WatchingResource),
            "Watching {ResourceType} starting from resource version {ResourceVersion}.",
            typeof(TResource).Name,
            _lastResourceVersion);

        // completion source helps turn OnClose callback into something awaitable
        var watcherCompletionSource = new TaskCompletionSource<int>();

        // begin watching where list left off
        var watchWithHttpMessage = RetrieveResourceListAsync(watch: true, resourceVersion: _lastResourceVersion, resourceSelector: _selector, cancellationToken: cancellationToken);

        var lastEventUtc = DateTime.UtcNow;
        using var watcher = watchWithHttpMessage.Watch<TResource, TListResource>(
            (watchEventType, item) =>
            {
                if (!watcherCompletionSource.Task.IsCompleted)
                {
                    lastEventUtc = DateTime.UtcNow;
                    OnEvent(watchEventType, item);
                }
            },
            error =>
            {
                if (error is KubernetesException kubernetesError)
                {
                    // deal with this non-recoverable condition "too old resource version"
                    if (string.Equals(kubernetesError.Status.Reason, "Expired", StringComparison.Ordinal))
                    {
                        // cause this error to surface
                        watcherCompletionSource.TrySetException(error);
                        throw error;
                    }
                }

                Logger.LogDebug(
                    EventId(EventType.IgnoringError),
                    "Ignoring error {ErrorType}: {ErrorMessage}",
                    error.GetType().Name,
                    error.Message);
            },
            () =>
            {
                watcherCompletionSource.TrySetResult(0);
            });

        // reconnect if no events have arrived after a certain time
        using var checkLastEventUtcTimer = new Timer(
            _ =>
            {
                var lastEvent = DateTime.UtcNow - lastEventUtc;
                if (lastEvent > TimeSpan.FromMinutes(9.5))
                {
                    lastEventUtc = DateTime.MaxValue;
                    Logger.LogDebug(
                        EventId(EventType.DisposingToReconnect),
                        "Disposing watcher for {ResourceType} to cause reconnect.",
                        typeof(TResource).Name);

                    watcherCompletionSource.TrySetCanceled();
                    watcher.Dispose();
                }
            },
            state: null,
            dueTime: TimeSpan.FromSeconds(45),
            period: TimeSpan.FromSeconds(45));

        using var registration = cancellationToken.Register(watcher.Dispose);
        try
        {
            await watcherCompletionSource.Task;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnEvent(WatchEventType watchEventType, TResource item)
    {
        if (watchEventType != WatchEventType.Modified || item.Kind != "ConfigMap")
        {
            Logger.LogDebug(
                EventId(EventType.InformerWatchEvent),
                "Informer {ResourceType} received {WatchEventType} notification for {ItemKind}/{ItemName}.{ItemNamespace} at resource version {ResourceVersion}",
                typeof(TResource).Name,
                watchEventType,
                item.Kind,
                item.Name(),
                item.Namespace(),
                item.ResourceVersion());
        }

        if (watchEventType == WatchEventType.Added ||
            watchEventType == WatchEventType.Modified)
        {
            // BUGBUG: log warning if cache was not in expected state
            _cache[NamespacedName.From(item)] = item.Metadata?.OwnerReferences;
        }

        if (watchEventType == WatchEventType.Deleted)
        {
            _cache.Remove(NamespacedName.From(item));
        }

        if (watchEventType == WatchEventType.Added ||
            watchEventType == WatchEventType.Modified ||
            watchEventType == WatchEventType.Deleted ||
            watchEventType == WatchEventType.Bookmark)
        {
            _lastResourceVersion = item.ResourceVersion();
        }

        if (watchEventType == WatchEventType.Added ||
            watchEventType == WatchEventType.Modified ||
            watchEventType == WatchEventType.Deleted)
        {
            InvokeRegistrationCallbacks(watchEventType, item);
        }
    }

    private void InvokeRegistrationCallbacks(WatchEventType eventType, TResource resource)
    {

        List<Exception> innerExceptions = default;
        foreach (var registration in _registrations)
        {
            try
            {
                registration.Callback.Invoke(eventType, resource);
            }
            catch (Exception innerException)
            {
                innerExceptions ??= new List<Exception>();
                innerExceptions.Add(innerException);
            }
        }

        if (innerExceptions is not null)
        {
            throw new AggregateException("One or more exceptions thrown by ResourceInformerCallback.", innerExceptions);
        }
    }

    internal class Registration : IResourceInformerRegistration
    {
        private bool _disposedValue;

        public Registration(ResourceInformer<TResource, TListResource> resourceInformer, ResourceInformerCallback<TResource> callback)
        {
            ResourceInformer = resourceInformer;
            Callback = callback;
            lock (resourceInformer._sync)
            {
                resourceInformer._registrations = resourceInformer._registrations.Add(this);
            }
        }

        ~Registration()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public ResourceInformer<TResource, TListResource> ResourceInformer { get; }
        public ResourceInformerCallback<TResource> Callback { get; }

        public Task ReadyAsync(CancellationToken cancellationToken) => ResourceInformer.ReadyAsync(cancellationToken);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                lock (ResourceInformer._sync)
                {
                    ResourceInformer._registrations = ResourceInformer._registrations.Remove(this);
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
