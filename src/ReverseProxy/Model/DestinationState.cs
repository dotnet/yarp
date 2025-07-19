// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Model;

/// <summary>
/// Representation of a cluster's destination for use at runtime.
/// </summary>
public sealed class DestinationState : IReadOnlyList<DestinationState>
{
    private volatile DestinationModel _model = default!;

    /// <summary>
    /// Creates a new instance. This constructor is for tests and infrastructure, this type is normally constructed by
    /// the configuration loading infrastructure.
    /// </summary>
    public DestinationState(string destinationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationId);
        DestinationId = destinationId;
    }

    /// <summary>
    /// Constructor overload to additionally initialize the <see cref="DestinationModel"/> for tests and infrastructure,
    /// such as updating the <see cref="ReverseProxyFeature"/> via <see cref="HttpContextFeaturesExtensions"/>
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public DestinationState(string destinationId, DestinationModel model) : this(destinationId)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>
    /// The destination's unique id.
    /// </summary>
    public string DestinationId { get; }

    /// <summary>
    /// A snapshot of the current configuration
    /// </summary>
    public DestinationModel Model
    {
        get => _model;
        internal set => _model = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Mutable health state for this destination.
    /// </summary>
    public DestinationHealthState Health { get; } = new DestinationHealthState();

    /// <summary>
    /// Keeps track of the total number of concurrent requests on this endpoint.
    /// The setter should only be used for testing purposes.
    /// </summary>
    public int ConcurrentRequestCount
    {
        get => ConcurrencyCounter.Value;
        set => ConcurrencyCounter.Value = value;
    }

    internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

    DestinationState IReadOnlyList<DestinationState>.this[int index]
        => index == 0 ? this : throw new IndexOutOfRangeException();

    int IReadOnlyCollection<DestinationState>.Count => 1;

    private Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<DestinationState> IEnumerable<DestinationState>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private struct Enumerator : IEnumerator<DestinationState>
    {
        private bool _read;

        internal Enumerator(DestinationState instance)
        {
            Current = instance;
            _read = false;
        }

        public DestinationState Current { get; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_read)
            {
                _read = true;
                return true;
            }
            return false;
        }

        public void Dispose()
        {

        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }
    }
}
