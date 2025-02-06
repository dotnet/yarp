// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class AtomicCounter
{
    private int _value;

    /// <summary>
    /// Gets the current value of the counter.
    /// </summary>
    public int Value
    {
        get => Volatile.Read(ref _value);
        set => Volatile.Write(ref _value, value);
    }

    /// <summary>
    /// Atomically increments the counter value by 1.
    /// </summary>
    public int Increment()
    {
        return Interlocked.Increment(ref _value);
    }

    /// <summary>
    /// Atomically decrements the counter value by 1.
    /// </summary>
    public int Decrement()
    {
        return Interlocked.Decrement(ref _value);
    }

    /// <summary>
    /// Atomically resets the counter value to 0.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _value, 0);
    }
}
