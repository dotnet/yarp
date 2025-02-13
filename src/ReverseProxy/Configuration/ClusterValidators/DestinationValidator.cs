// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class DestinationValidator : IClusterValidator
{
    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors)
    {
        if (cluster.Destinations is null)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var (name, destination) in cluster.Destinations)
        {
            if (string.IsNullOrEmpty(destination.Address))
            {
                errors.Add(new ArgumentException($"No address found for destination '{name}' on cluster '{cluster.ClusterId}'."));
            }
        }

        return ValueTask.CompletedTask;
    }
}
