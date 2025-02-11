// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

/// <summary>
/// Provides method to validate cluster configuration.
/// </summary>
public interface IClusterValidator
{

    /// <summary>
    /// Perform validation on a cluster configuration by adding exceptions to the provided collection.
    /// </summary>
    /// <param name="cluster">Cluster configuration to validate</param>
    /// <param name="errors">Collection of all validation exceptions</param>
    /// <returns>A ValueTask representing the asynchronous validation operation.</returns>
    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors);
}
