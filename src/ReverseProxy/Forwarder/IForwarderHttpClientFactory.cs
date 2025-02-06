// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Provides a method to create instances of <see cref="HttpMessageInvoker"/>
/// for forwarding requests to an upstream server.
/// </summary>
/// <remarks>
/// This is somewhat similarly to `System.Net.Http.IHttpClientFactory`,
/// except that this factory class is meant for direct use,
/// which the forwarder requires in order to keep separate pools for each cluster.
/// </remarks>
public interface IForwarderHttpClientFactory
{
    /// <summary>
    /// Creates and configures an <see cref="HttpMessageInvoker"/> instance
    /// that can be used for forwarding requests to an upstream server.
    /// </summary>
    /// <param name="context">An <see cref="ForwarderHttpClientContext"/> carrying old and new cluster configurations.</param>
    /// <remarks>
    /// <para>
    /// A call to <see cref="CreateClient(ForwarderHttpClientContext)"/> can return either
    /// a new <see cref="HttpMessageInvoker"/> instance or an old one if the configuration has not changed.
    /// If the old configuration is null, a new <see cref="HttpMessageInvoker"/> is always created.
    /// The returned <see cref="HttpMessageInvoker"/> instance MUST NOT be disposed
    /// because it can be used concurrently by several in-flight requests.
    /// </para>
    /// </remarks>
    HttpMessageInvoker CreateClient(ForwarderHttpClientContext context);
}
