// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Queues;

/// <summary>
/// Class WorkQueue is the default implementation of a work queue.
/// Implements the <see cref="IWorkQueue{TItem}" />.
/// </summary>
/// <typeparam name="TItem">The type of the t item.</typeparam>
/// <seealso cref="IWorkQueue{TItem}" />
public class WorkQueue<TItem> : IWorkQueue<TItem>
{
    private readonly object _sync = new object();
    private readonly Dictionary<TItem, object> _dirty = new Dictionary<TItem, object>();
    private readonly Dictionary<TItem, object> _processing = new Dictionary<TItem, object>();
    private readonly Queue<TItem> _queue = new Queue<TItem>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private readonly CancellationTokenSource _shuttingDown = new CancellationTokenSource();
    private bool _disposedValue; // To detect redundant calls

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(TItem item)
    {
        lock (_sync)
        {
            if (_shuttingDown.IsCancellationRequested)
            {
                return;
            }

            if (_dirty.ContainsKey(item))
            {
                return;
            }

            _dirty.Add(item, null);
            if (_processing.ContainsKey(item))
            {
                return;
            }

            _queue.Enqueue(item);
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the specified cancellation token.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task&lt;System.ValueTuple&lt;TItem, System.Boolean&gt;&gt;.</returns>
    public async Task<(TItem item, bool shutdown)> GetAsync(CancellationToken cancellationToken)
    {
        using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shuttingDown.Token))
        {
            try
            {
                await _semaphore.WaitAsync(linkedTokenSource.Token);

                await OnGetAsync(linkedTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                if (_shuttingDown.IsCancellationRequested)
                {
                    return (default, true);
                }

                throw;
            }
        }

        lock (_sync)
        {
            if (_queue.Count == 0 || _shuttingDown.IsCancellationRequested)
            {
                _semaphore.Release();
                return (default, true);
            }

            var item = _queue.Dequeue();

            _processing.Add(item, null);
            _dirty.Remove(item);

            return (item, false);
        }
    }

    /// <summary>
    /// Done the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Done(TItem item)
    {
        lock (_sync)
        {
            _processing.Remove(item);
            if (_dirty.ContainsKey(item))
            {
                _queue.Enqueue(item);
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Gets the queue length of this instance.
    /// </summary>
    public int Len()
    {
        lock (_sync)
        {
            return _queue.Count;
        }
    }

    /// <summary>
    /// Shuts down.
    /// </summary>
    public void ShutDown()
    {
        lock (_sync)
        {
            _shuttingDown.Cancel();
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Shutting down.
    /// </summary>
    /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
    public bool ShuttingDown()
    {
        return _shuttingDown.IsCancellationRequested;
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Called in GetAsync BEFORE the items is dequeued to allow rate-limiting of processing.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation</param>
    /// <returns>A task.</returns>
    protected virtual Task OnGetAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
