// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal abstract class EventListenerService<TService, TTelemetryConsumer, TMetrics> : EventListener, IHostedService
    where TMetrics : class, new()
{
    protected abstract string EventSourceName { get; }
    protected abstract int NumberOfMetrics { get; }
    protected abstract void OnEvent(TTelemetryConsumer[] consumers, EventWrittenEventArgs eventData);
    protected abstract bool TrySaveMetric(TMetrics metrics, string name, double value);

    private readonly ILogger<TService> _logger;
    private readonly TTelemetryConsumer[]? _telemetryConsumers;
    private readonly IMetricsConsumer<TMetrics>[]? _metricsConsumers;

    private int _metricsCount;
    private TMetrics? _previousMetrics;
    private TMetrics? _currentMetrics;

    private EventSource? _eventSource;
    private readonly object _syncObject = new();
    private readonly bool _initialized;

    public EventListenerService(
        ILogger<TService> logger,
        IEnumerable<TTelemetryConsumer> telemetryConsumers,
        IEnumerable<IMetricsConsumer<TMetrics>> metricsConsumers)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(telemetryConsumers);
        ArgumentNullException.ThrowIfNull(metricsConsumers);

        _logger = logger;
        _telemetryConsumers = telemetryConsumers.ToArray();
        _metricsConsumers = metricsConsumers.ToArray();

        if (_telemetryConsumers.Any(s => s is null) || metricsConsumers.Any(c => c is null))
        {
            throw new ArgumentException("A consumer may not be null",
                _telemetryConsumers.Any(s => s is null) ? nameof(telemetryConsumers) : nameof(metricsConsumers));
        }

        if (_telemetryConsumers.Length == 0)
        {
            _telemetryConsumers = null;
        }

        if (_metricsConsumers.Length == 0)
        {
            _metricsConsumers = null;
        }

        lock (_syncObject)
        {
            if (_eventSource is EventSource eventSource)
            {
                EnableEventSource(eventSource);
            }

            _initialized = true;
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == EventSourceName)
        {
            lock (_syncObject)
            {
                _eventSource = eventSource;

                if (_initialized)
                {
                    // Ctor already finished - enable the EventSource here
                    EnableEventSource(eventSource);
                }
            }
        }
    }

    private void EnableEventSource(EventSource eventSource)
    {
        var enableEvents = _telemetryConsumers is not null;
        var enableMetrics = _metricsConsumers is not null;

        if (!enableEvents && !enableMetrics)
        {
            return;
        }

        var eventLevel = enableEvents ? EventLevel.Informational : EventLevel.Critical;
        var arguments = enableMetrics ? new Dictionary<string, string?> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } } : null;

        EnableEvents(eventSource, eventLevel, EventKeywords.None, arguments);
    }

    protected sealed override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId <= 0)
        {
            OnNonUserEvent(eventData);
        }
        else if (_telemetryConsumers is TTelemetryConsumer[] consumers)
        {
            OnEvent(consumers, eventData);
        }
    }

    private void OnNonUserEvent(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == -1)
        {
            if (!ReferenceEquals(eventData.EventSource, _eventSource))
            {
                // Workaround for https://github.com/dotnet/runtime/issues/31927
                // EventCounters are published to all EventListeners, regardless of
                // which EventSource providers a listener is enabled for.
                return;
            }

            // Throwing an exception here would crash the process
            if (eventData.EventName != "EventCounters" ||
                eventData.Payload?.Count != 1 ||
                eventData.Payload[0] is not IDictionary<string, object> counters ||
                !counters.TryGetValue("Name", out var nameObject) ||
                nameObject is not string name ||
                !(counters.TryGetValue("Mean", out var valueObj) || counters.TryGetValue("Increment", out valueObj)) ||
                valueObj is not double value)
            {
                _logger.LogDebug("Failed to parse EventCounters event from {EventSourceName}", EventSourceName);
                return;
            }

            var metrics = _currentMetrics ??= new();

            if (!TrySaveMetric(metrics, name, value))
            {
                return;
            }

            if (++_metricsCount == NumberOfMetrics)
            {
                _metricsCount = 0;

                var previous = _previousMetrics;
                _previousMetrics = metrics;
                _currentMetrics = null;

                if (previous is null)
                {
                    return;
                }

                if (_metricsConsumers is IMetricsConsumer<TMetrics>[] consumers)
                {
                    foreach (var consumer in consumers)
                    {
                        try
                        {
                            consumer.OnMetrics(previous, metrics);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Uncaught exception occurred while processing metrics for EventSource {EventSourceName}", EventSourceName);
                        }
                    }
                }
            }
        }
        else if (eventData.EventId == 0)
        {
            _logger.LogError("Received an error message from EventSource {EventSourceName}: {Message}", EventSourceName, eventData.Message);
        }
        else
        {
            _logger.LogDebug("Received an unknown event from EventSource {EventSourceName}: {EventId}", EventSourceName, eventData.EventId);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
