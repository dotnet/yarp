// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// A data source for proxy route and cluster information.
/// </summary>
public interface IProxyConfigProvider
{
    /// <summary>
    /// Returns the current route and cluster data.
    /// </summary>
    /// <returns></returns>
    IProxyConfig GetConfig();
}
