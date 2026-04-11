// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Test hook that allows WebApplicationFactory to inject configuration
/// before the app binds its config model.
/// Uses AsyncLocal to flow state from test to app without shared mutable state.
/// See: https://github.com/dotnet/aspnetcore/issues/37680
/// </summary>
internal static class TestConfiguration
{
    internal static readonly AsyncLocal<Action<IConfigurationBuilder>?> _current = new();

    public static IConfigurationBuilder AddTestConfiguration(this IConfigurationBuilder configurationBuilder)
    {
        if (_current.Value is { } configure)
        {
            configure(configurationBuilder);
        }

        return configurationBuilder;
    }

    public static void Create(Action<IConfigurationBuilder> action)
    {
        _current.Value = action;
    }
}
