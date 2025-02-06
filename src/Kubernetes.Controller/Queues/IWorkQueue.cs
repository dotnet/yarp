// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Queues;

/// <summary>
/// Interface IWorkQueue holds a series of work item objects. When objects are removed from the queue they are noted
/// as well in a processing set. If new items arrive while processing they are not added to the queue until
/// the processing of that item is <see cref="Done" />. In this way processing the same item twice simultaneously due to
/// incoming event notifications is not possible.
/// Ported from https://github.com/kubernetes/client-go/blob/master/util/workqueue/queue.go.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
public interface IWorkQueue<TItem> : IDisposable
{
    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    void Add(TItem item);

    /// <summary>
    /// Returns number of items actively waiting in queue.
    /// </summary>
    /// <returns>System.Int32.</returns>
    int Len();

    /// <summary>
    /// Gets the next item in the queue once it is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task&lt;System.ValueTuple&lt;TItem, System.Boolean&gt;&gt;.</returns>
    Task<(TItem item, bool shutdown)> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after <see cref="GetAsync"/> to inform the queue that the item
    /// processing is complete.
    /// </summary>
    /// <param name="item">The item.</param>
    void Done(TItem item);

    /// <summary>
    /// Shuts down.
    /// </summary>
    void ShutDown();

    /// <summary>
    /// Shutting down.
    /// </summary>
    /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
    bool ShuttingDown();
}
