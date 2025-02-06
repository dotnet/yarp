// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Routing;

/// <summary>
/// Represents request header metadata used during routing.
/// </summary>
internal interface IHeaderMetadata
{
    /// <summary>
    /// One or more matchers to apply to the request headers.
    /// </summary>
    HeaderMatcher[] Matchers { get; }
}
