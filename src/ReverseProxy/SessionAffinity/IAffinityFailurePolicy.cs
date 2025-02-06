// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Affinity failures handling policy.
/// </summary>
public interface IAffinityFailurePolicy
{
    /// <summary>
    ///  A unique identifier for this failure policy. This will be referenced from config.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Handles affinity failures. This method assumes the full control on <see cref="HttpContext"/>
    /// and can change it in any way.
    /// </summary>
    /// <param name="context">Current request's context.</param>
    /// <param name="cluster">The associated cluster for the request.</param>
    /// <param name="affinityStatus">Affinity resolution status.</param>
    /// <returns>
    /// 'true' if the failure is considered recoverable and the request processing can proceed.
    /// Otherwise, 'false' indicating that an error response has been generated and the request's processing must be terminated.
    /// </returns>
    Task<bool> Handle(HttpContext context, ClusterState cluster, AffinityStatus affinityStatus);
}
