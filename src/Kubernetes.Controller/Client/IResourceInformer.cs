// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Callback for resource event notifications.
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IKubernetesObject{V1ObjectMeta}"/> being monitored.</typeparam>
/// <param name="eventType">The type of change event which was received.</param>
/// <param name="resource">The instance of the resource which was received.</param>
public delegate void ResourceInformerCallback<TResource>(WatchEventType eventType, TResource resource) where TResource : class, IKubernetesObject<V1ObjectMeta>;

/// <summary>
/// Interface IResourceInformer is a service which generates
/// notifications for a specific type
/// of Kubernetes object. The callback eventType informs if the notification
/// is because it is new, modified, or has been deleted.
/// Implements the <see cref="IHostedService" />.
/// </summary>
/// <typeparam name="TResource">The type of the t resource.</typeparam>
/// <seealso cref="IObservable{T}" />
/// <seealso cref="IHostedService" />
public interface IResourceInformer<TResource> : IHostedService, IResourceInformer
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
{
    /// <summary>
    /// Registers a callback for change notification. To ensure no events are missed the registration
    /// may be created in the constructor of a dependant <see cref="IHostedService"/>. The returned
    /// registration should be disposed when the receiver is ending its work.
    /// </summary>
    /// <param name="callback">The delegate that is invoked with each resource notification.</param>
    /// <returns>A registration that should be disposed to end the notifications.</returns>
    IResourceInformerRegistration Register(ResourceInformerCallback<TResource> callback);
}

public interface IResourceInformer
{
    /// <summary>
    /// Instructs the resource informer to being watching resources. Allows the startup of informers to be synchronised.
    /// </summary>
    void StartWatching();

    /// <summary>
    /// Returns a task that can be awaited to know when the initial listing of resources is complete.
    /// Once an await on this method is completed it is safe to assume that all the knowledge of this resource
    /// type has been made available. Any new changes will be a result of receiving new updates.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task.</returns>
    Task ReadyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers a callback for change notification. To ensure no events are missed the registration
    /// may be created in the constructor of a dependant <see cref="IHostedService"/>. The returned
    /// registration should be disposed when the receiver is ending its work.
    /// </summary>
    /// <param name="callback">The delegate that is invoked with each resource notification.</param>
    /// <returns>A registration that should be disposed to end the notifications.</returns>
    IResourceInformerRegistration Register(ResourceInformerCallback<IKubernetesObject<V1ObjectMeta>> callback);
}
