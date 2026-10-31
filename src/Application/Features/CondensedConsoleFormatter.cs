// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Yarp.Application.Features;

/// <summary>
/// Wraps the built-in "simple" console formatter and drops only the verbose HttpForwarder
/// forwarding error (event 48), which is replaced by the single-line summary written by
/// <see cref="RequestLoggingFeature"/>. Everything else is formatted exactly as before.
/// Being a console formatter, it does not affect any other logging provider.
/// </summary>
internal sealed class CondensedConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "yarp-condensed";

    // Yarp.ReverseProxy.Utilities.EventIds.ForwardingError (internal to the ReverseProxy library).
    private const int ForwardingErrorEventId = 48;
    private const string HttpForwarderCategory = "Yarp.ReverseProxy.Forwarder.HttpForwarder";

    private readonly IServiceProvider _services;
    private ConsoleFormatter? _inner;

    public CondensedConsoleFormatter(IServiceProvider services) : base(FormatterName)
    {
        _services = services;
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (logEntry.EventId.Id == ForwardingErrorEventId && logEntry.Category == HttpForwarderCategory)
        {
            return;
        }

        // Resolved lazily: the formatters are created by the console provider, which would
        // otherwise make this formatter depend on itself.
        _inner ??= _services.GetServices<ConsoleFormatter>()
            .First(f => string.Equals(f.Name, ConsoleFormatterNames.Simple, StringComparison.OrdinalIgnoreCase));

        _inner.Write(in logEntry, scopeProvider, textWriter);
    }
}
