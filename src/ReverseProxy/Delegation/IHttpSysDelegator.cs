// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Delegation;

public interface IHttpSysDelegator
{
    /// <summary>
    /// Disposes the handle to the given queue if it exists.
    /// </summary>
    /// <remarks>
    /// If any destinations still reference the queue, the handle will be
    /// re-created the next time a request is routed to one of the destinations.
    /// </remarks>
    /// <param name="queueName">The name of the queue to reset.</param>
    /// <param name="urlPrefix">The url prefix of the queue to reset.</param>
    void ResetQueue(string queueName, string urlPrefix);
}
