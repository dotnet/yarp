// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.ReverseProxy.Management;

/// <summary>
/// Reverse Proxy builder for DI configuration.
/// </summary>
internal sealed class ReverseProxyBuilder : IReverseProxyBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReverseProxyBuilder"/> class.
    /// </summary>
    /// <param name="services">Services collection.</param>
    public ReverseProxyBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>
    /// Gets the services collection.
    /// </summary>
    public IServiceCollection Services { get; }
}
