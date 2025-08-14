// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Yarp.Kubernetes.Tests.Utils;

public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    private readonly LogLevel _minLogLevel;

    public TestLogger(ITestOutputHelper output, LogLevel minLogLevel = LogLevel.Debug)
    {
        _output = output;
        _minLogLevel = minLogLevel;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _minLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _output.WriteLine(formatter.Invoke(state, exception));
    }
}
