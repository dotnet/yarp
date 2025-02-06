// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client.Tests;

internal class SyncResourceInformer<TResource> : IResourceInformer<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
{
    private readonly object _sync = new();
    private ImmutableList<Registration> _registrations = ImmutableList<Registration>.Empty;

    public void PublishUpdate(WatchEventType eventType, TResource resource)
    {
        List<ResourceInformerCallback<TResource>> callbacks;
        lock (_sync)
        {
            callbacks = _registrations.Select(x => x.Callback).ToList();
        }

        callbacks.ForEach(x => x.Invoke(eventType, resource));
    }

    public Task ReadyAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public IResourceInformerRegistration Register(ResourceInformerCallback<TResource> callback)
    {
        return new Registration(this, callback);
    }

    public IResourceInformerRegistration Register(ResourceInformerCallback<IKubernetesObject<V1ObjectMeta>> callback)
    {
        return new Registration(this, (eventType, resource) => callback(eventType, resource));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void StartWatching()
    {
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal class Registration : IResourceInformerRegistration
    {
        private bool _disposedValue;

        public Registration(SyncResourceInformer<TResource> resourceInformer, ResourceInformerCallback<TResource> callback)
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

        public SyncResourceInformer<TResource> ResourceInformer { get; }
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
