// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Allows subscribing to events notifying you when the configuration is loaded and applied, or when those actions fail.
/// </summary>
public interface IConfigChangeListener
{
    /// <summary>
    /// Invoked when an error occurs while loading the configuration.
    /// </summary>
    /// <param name="configProvider">The instance of the configuration provider that failed to provide the configuration.</param>
    /// <param name="exception">The thrown exception.</param>
    void ConfigurationLoadingFailed(IProxyConfigProvider configProvider, Exception exception);

    /// <summary>
    /// Invoked once the configuration have been successfully loaded.
    /// </summary>
    /// <param name="proxyConfigs">The list of instances that have been loaded.</param>
    void ConfigurationLoaded(IReadOnlyList<IProxyConfig> proxyConfigs);

    /// <summary>
    /// Invoked when an error occurs while applying the configuration.
    /// </summary>
    /// <param name="proxyConfigs">The list of instances that were being processed.</param>
    /// <param name="exception">The thrown exception.</param>
    void ConfigurationApplyingFailed(IReadOnlyList<IProxyConfig> proxyConfigs, Exception exception);

    /// <summary>
    /// Invoked once the configuration has been successfully applied.
    /// </summary>
    /// <param name="proxyConfigs">The list of instances that have been applied.</param>
    void ConfigurationApplied(IReadOnlyList<IProxyConfig> proxyConfigs);
}
