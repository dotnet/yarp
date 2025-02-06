// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Yarp.Kubernetes.Controller.Rate;

/// <summary>
/// Struct Limit defines the maximum frequency of some events.
/// Limit is represented as number of events per second.
/// A zero Limit allows no events.
/// https://github.com/golang/time/blob/master/rate/rate.go#L19
/// Implements the <see cref="IEquatable{T}" />.
/// </summary>
/// <seealso cref="IEquatable{T}" />
public struct Limit : IEquatable<Limit>
{
    private readonly double _tokensPerSecond;

    /// <summary>
    /// Initializes a new instance of the <see cref="Limit"/> struct.
    /// </summary>
    /// <param name="perSecond">The per second.</param>
    public Limit(double perSecond)
    {
        _tokensPerSecond = perSecond;
    }

    /// <summary>
    /// Gets a predefined maximum <see cref="Limit"/>.
    /// </summary>
    /// <value>The maximum.</value>
    public static Limit Max { get; } = new Limit(double.MaxValue);

    /// <summary>
    /// Implements the == operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator ==(Limit left, Limit right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Implements the != operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator !=(Limit left, Limit right)
    {
        return !(left == right);
    }

    /// <summary>
    /// TokensFromDuration is a unit conversion function from a time duration to the number of tokens
    /// which could be accumulated during that duration at a rate of limit tokens per second.
    /// https://github.com/golang/time/blob/master/rate/rate.go#L396.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>System.Double.</returns>
    public double TokensFromDuration(TimeSpan duration)
    {
        var sec = duration.Ticks / TimeSpan.TicksPerSecond * _tokensPerSecond;
        var nsec = duration.Ticks % TimeSpan.TicksPerSecond * _tokensPerSecond;
        return sec + nsec / TimeSpan.TicksPerSecond;
    }

    /// <summary>
    /// Durations from tokens is a unit conversion function from the number of tokens to the duration
    /// of time it takes to accumulate them at a rate of limit tokens per second.
    /// https://github.com/golang/time/blob/master/rate/rate.go#L389.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>TimeSpan.</returns>
    public TimeSpan DurationFromTokens(double tokens)
    {
        return TimeSpan.FromSeconds(tokens / _tokensPerSecond);
    }

    /// <summary>
    /// Determines whether the specified <see cref="object" /> is equal to this instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
    public override bool Equals(object obj)
    {
        return obj is Limit limit && Equals(limit);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><see langword="true" /> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <see langword="false" />.</returns>
    public bool Equals([AllowNull] Limit other)
    {
        return _tokensPerSecond == other._tokensPerSecond;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_tokensPerSecond);
    }
}
