// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Yarp.ReverseProxy.Common;

public sealed class TestLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    private readonly XunitLoggerProvider _xunitLoggerProvider = new(output);

    public ILogger CreateLogger(string categoryName) => new TestLogger(_xunitLoggerProvider.CreateLogger(categoryName), categoryName);

    public void Dispose() => _xunitLoggerProvider.Dispose();
}
