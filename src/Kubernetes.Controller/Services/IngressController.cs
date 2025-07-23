// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Client;
using Yarp.Kubernetes.Controller.Hosting;
using Yarp.Kubernetes.Controller.Queues;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// Controller receives notifications from informers. The data which is needed for processing is
/// saved in an <see cref="ICache"/> instance and resources which need to be reconciled are
/// added to an <see cref="ProcessingRateLimitedQueue{QueueItem}"/>. The background task dequeues
/// items and passes them to an <see cref="IReconciler"/> service for processing.
/// </summary>
public class IngressController : BackgroundHostedService
{
    private readonly IReadOnlyList<IResourceInformerRegistration> _registrations;
    private readonly ICache _cache;
    private readonly IReconciler _reconciler;

    private bool _registrationsReady;
    private readonly WorkQueue<QueueItem> _queue;
    private readonly QueueItem _ingressChangeQueueItem;

    public IngressController(
        ICache cache,
        IReconciler reconciler,
        IResourceInformer<V1Ingress> ingressInformer,
        IResourceInformer<V1Service> serviceInformer,
        IResourceInformer<V1Endpoints> endpointsInformer,
        IResourceInformer<V1IngressClass> ingressClassInformer,
        IResourceInformer<V1Secret> secretInformer,
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<YarpOptions> options,
        ILogger<IngressController> logger)
        : base(hostApplicationLifetime, logger)
    {
        ArgumentNullException.ThrowIfNull(ingressInformer, nameof(ingressInformer));
        ArgumentNullException.ThrowIfNull(serviceInformer, nameof(serviceInformer));
        ArgumentNullException.ThrowIfNull(endpointsInformer, nameof(endpointsInformer));
        ArgumentNullException.ThrowIfNull(ingressClassInformer, nameof(ingressClassInformer));
        ArgumentNullException.ThrowIfNull(secretInformer, nameof(secretInformer));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        var watchSecrets = options.Value.ServerCertificates;

        var registrations = new List<IResourceInformerRegistration>()
        {
            serviceInformer.Register(Notification),
            endpointsInformer.Register(Notification),
            ingressClassInformer.Register(Notification),
            ingressInformer.Register(Notification)
        };

        if (watchSecrets)
        {
            registrations.Add(secretInformer.Register(Notification));
        }

        _registrations = registrations;

        _registrationsReady = false;
        serviceInformer.StartWatching();
        endpointsInformer.StartWatching();
        ingressClassInformer.StartWatching();
        ingressInformer.StartWatching();

        if (watchSecrets)
        {
            secretInformer.StartWatching();
        }

        _queue = new ProcessingRateLimitedQueue<QueueItem>(perSecond: 0.5, burst: 1);

        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(reconciler);

        _cache = cache;
        _reconciler = reconciler;

        _ingressChangeQueueItem = new QueueItem("Ingress Change");
    }

    /// <summary>
    /// Disconnects from resource informers, and cause queue to become shut down.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _queue.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernetes API server.</param>
    private void Notification(WatchEventType eventType, V1Ingress resource)
    {
        if (_cache.Update(eventType, resource))
        {
            NotificationIngressChanged();
        }
    }

    private void NotificationIngressChanged()
    {
        if (!_registrationsReady)
        {
            return;
        }

        _queue.Add(_ingressChangeQueueItem);
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernetes API server.</param>
    private void Notification(WatchEventType eventType, V1Service resource)
    {
        var ingressNames = _cache.Update(eventType, resource);
        if (ingressNames.Count > 0)
        {
            NotificationIngressChanged();
        }
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernetes API server.</param>
    private void Notification(WatchEventType eventType, V1Endpoints resource)
    {
        var ingressNames = _cache.Update(eventType, resource);
        if (ingressNames.Count > 0)
        {
            NotificationIngressChanged();
        }
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernetes API server.</param>
    private void Notification(WatchEventType eventType, V1IngressClass resource)
    {
        _cache.Update(eventType, resource);
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernetes API server.</param>
    private void Notification(WatchEventType eventType, V1Secret resource)
    {
        _cache.Update(eventType, resource);
    }

    /// <summary>
    /// Called once at startup by the hosting infrastructure. This function must remain running
    /// for the entire lifetime of an application.
    /// </summary>
    /// <param name="cancellationToken">Indicates when the web application is shutting down.</param>
    /// <returns>The Task representing the async function results.</returns>
    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        // First wait for all informers to fully List resources before processing begins.
        foreach (var registration in _registrations)
        {
            await registration.ReadyAsync(cancellationToken).ConfigureAwait(false);
        }

        // At this point we know that all the Ingress and Endpoint caches are at least in sync
        // with cluster's state as of the start of this controller.
        _registrationsReady = true;
        NotificationIngressChanged();

        // Now begin one loop to process work until an application shutdown is requested.
        while (!cancellationToken.IsCancellationRequested)
        {
            // Dequeue the next item to process
            var (item, shutdown) = await _queue.GetAsync(cancellationToken).ConfigureAwait(false);
            if (shutdown)
            {
                Logger.LogInformation("Work queue has been shutdown. Exiting reconciliation loop.");
                return;
            }

            try
            {
                await _reconciler.ProcessAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Logger.LogInformation("Rescheduling {Change}", item.Change);

                // Any failure to process this item results in being re-queued
                _queue.Add(item);
            }
            finally
            {
                _queue.Done(item);
            }
        }

        Logger.LogInformation("Reconciliation loop cancelled");
    }
}
