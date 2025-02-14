// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Rate;

/// <summary>
/// <para>
/// Class Limiter controls how frequently events are allowed to happen.
/// It implements a "token bucket" of size b, initially full and refilled
/// at rate r tokens per second.
/// Informally, in any large enough time interval, the Limiter limits the
/// rate to r tokens per second, with a maximum burst size of b events.
/// As a special case, if r == Inf (the infinite rate), b is ignored.
/// See https://en.wikipedia.org/wiki/Token_bucket for more about token buckets.
/// </para>
/// <para>
/// The zero value is a valid Limiter, but it will reject all events.
/// Use NewLimiter to create non-zero Limiters.
/// </para>
/// <para>
/// Limiter has three main methods, Allow, Reserve, and Wait.
/// Most callers should use Wait.
/// </para>
/// <para>
/// Each of the three methods consumes a single token.
/// They differ in their behavior when no token is available.
/// If no token is available, Allow returns false.
/// If no token is available, Reserve returns a reservation for a future token
/// and the amount of time the caller must wait before using it.
/// If no token is available, Wait blocks until one can be obtained
/// or its associated context.Context is canceled.
/// The methods AllowN, ReserveN, and WaitN consume n tokens.
/// </para>
/// https://github.com/golang/time/blob/master/rate/rate.go#L55.
/// </summary>
public class Limiter
{
    private readonly object _sync = new object();
    private readonly Limit _limit;
    private readonly TimeProvider _timeProvider;
    private readonly int _burst;
    private double _tokens;

    /// <summary>
    /// The last time the limiter's tokens field was updated.
    /// </summary>
    private DateTimeOffset _last;

    /// <summary>
    /// the latest time of a rate-limited event (past or future).
    /// </summary>
    private DateTimeOffset _lastEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="Limiter" /> class.
    /// Allows events up to <see cref="Limit" /><paramref name="limit" /> and permits bursts of
    /// at most <paramref name="burst" /> tokens.
    /// </summary>
    /// <param name="limit">The count per second which is allowed.</param>
    /// <param name="burst">The burst.</param>
    /// <param name="timeProvider">Accessor for the current UTC time.</param>
    public Limiter(Limit limit, int burst, TimeProvider timeProvider = default)
    {
        _limit = limit;
        _burst = burst;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Checks if a token is available effective immediately. If so, it is consumed.
    /// </summary>
    /// <returns><c>true</c> if a token is available and used, <c>false</c> otherwise.</returns>
    public bool Allow()
    {
        return AllowN(_timeProvider.GetUtcNow(), 1);
    }

    /// <summary>
    /// Checks if a number of tokens are available by a given time.
    /// They are consumed if available.
    /// </summary>
    /// <param name="now">The now.</param>
    /// <param name="number">The number.</param>
    /// <returns><c>true</c> if a number token is available and used, <c>false</c> otherwise.</returns>
    public bool AllowN(DateTimeOffset now, int number)
    {
        return ReserveImpl(now, number, TimeSpan.Zero).Ok;
    }

    /// <summary>
    /// Reserves this instance.
    /// </summary>
    /// <returns>Reservation.</returns>
    public Reservation Reserve()
    {
        return Reserve(_timeProvider.GetUtcNow(), 1);
    }

    /// <summary>
    /// ReserveN returns a Reservation that indicates how long the caller must wait before n events happen.
    /// The Limiter takes this Reservation into account when allowing future events.
    /// The returned Reservation’s OK() method returns false if n exceeds the Limiter's burst size.
    /// Usage example:
    /// r := lim.ReserveN(time.Now(), 1)
    /// if !r.OK() {
    /// return
    /// }
    /// time.Sleep(r.Delay())
    /// Act()
    /// Use this method if you wish to wait and slow down in accordance with the rate limit without dropping events.
    /// If you need to respect a deadline or cancel the delay, use Wait instead.
    /// To drop or skip events exceeding rate limit, use Allow instead.
    /// </summary>
    /// <param name="now">The now.</param>
    /// <param name="count">The number.</param>
    /// <returns>Reservation.</returns>
    public Reservation Reserve(DateTimeOffset now, int count)
    {
        return ReserveImpl(now, count, TimeSpan.MaxValue);
    }

    /// <summary>
    /// Waits the asynchronous.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Task.</returns>
    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return WaitAsync(1, cancellationToken);
    }

    /// <summary>
    /// wait as an asynchronous operation.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="Exception">rate: Wait(count={count}) exceeds limiter's burst {burst}.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task WaitAsync(int count, CancellationToken cancellationToken)
    {
        // https://github.com/golang/time/blob/master/rate/rate.go#L226
        int burst = default;
        Limit limit = default;
        lock (_sync)
        {
            burst = _burst;
            limit = _limit;
        }

        if (count > burst && limit != Limit.Max)
        {
            throw new Exception($"rate: Wait(count={count}) exceeds limiter's burst {burst}");
        }

        // Check if ctx is already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // Determine wait limit
        var waitLimit = limit.DurationFromTokens(count);

        while (true)
        {
            var now = _timeProvider.GetUtcNow();
            var r = ReserveImpl(now, count, waitLimit);
            if (r.Ok)
            {
                var delay = r.DelayFrom(now);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            await Task.Delay(waitLimit, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// reserveN is a helper method for AllowN, ReserveN, and WaitN.
    /// maxFutureReserve specifies the maximum reservation wait duration allowed.
    /// reserveN returns Reservation, not *Reservation, to avoid allocation in AllowN and WaitN.
    /// </summary>
    /// <param name="now">The now.</param>
    /// <param name="number">The number.</param>
    /// <param name="maxFutureReserve">The maximum future reserve.</param>
    /// <returns>Reservation.</returns>
    private Reservation ReserveImpl(DateTimeOffset now, int number, TimeSpan maxFutureReserve)
    {
        lock (_sync)
        {
            if (_limit == Limit.Max)
            {
                return new Reservation(
                    timeProvider: _timeProvider,
                    limiter: this,
                    ok: true,
                    tokens: number,
                    timeToAct: now);
            }

            var (newNow, last, tokens) = Advance(now);
            now = newNow;

            // Calculate the remaining number of tokens resulting from the request.
            tokens -= number;

            // Calculate the wait duration
            TimeSpan waitDuration = default;
            if (tokens < 0)
            {
                waitDuration = _limit.DurationFromTokens(-tokens);
            }

            // Decide result
            var ok = number <= _burst && waitDuration <= maxFutureReserve;

            // Prepare reservation
            if (ok)
            {
                var reservation = new Reservation(
                    timeProvider: _timeProvider,
                    limiter: this,
                    ok: true,
                    tokens: number,
                    limit: _limit,
                    timeToAct: now.Add(waitDuration));

                _last = newNow;
                _tokens = tokens;
                _lastEvent = reservation.TimeToAct;

                return reservation;
            }
            else
            {
                var reservation = new Reservation(
                    timeProvider: _timeProvider,
                    limiter: this,
                    ok: false,
                    limit: _limit);

                _last = last;

                return reservation;
            }
        }
    }

    /// <summary>
    /// advance calculates and returns an updated state for lim resulting from the passage of time.
    /// lim is not changed.
    /// advance requires that lim.mu is held.
    /// </summary>
    /// <param name="now">The now.</param>
    private (DateTimeOffset newNow, DateTimeOffset newLast, double newTokens) Advance(DateTimeOffset now)
    {
        lock (_sync)
        {
            var last = _last;
            if (now < last)
            {
                last = now;
            }

            // Avoid making delta overflow below when last is very old.
            var maxElapsed = _limit.DurationFromTokens(_burst - _tokens);
            var elapsed = now - last;
            if (elapsed > maxElapsed)
            {
                elapsed = maxElapsed;
            }

            // Calculate the new number of tokens, due to time that passed.
            var delta = _limit.TokensFromDuration(elapsed);
            var tokens = _tokens + delta;
            if (tokens > _burst)
            {
                tokens = _burst;
            }

            return (now, last, tokens);
        }
    }
}
