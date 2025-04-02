// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Common;

public sealed class TestLogger(ILogger xunitLogger, string categoryName) : ILogger
{
    public record LogEntry(string CategoryName, LogLevel LogLevel, EventId EventId, string Message, Exception Exception);

    private static readonly AsyncLocal<List<LogEntry>> _logsAsyncLocal = new();

    public static List<LogEntry> Collect() => _logsAsyncLocal.Value ??= [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => xunitLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _logsAsyncLocal.Value?.Add(new LogEntry(categoryName, logLevel, eventId, formatter(state, exception), exception));

        xunitLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}
