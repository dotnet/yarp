// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class ProxyHttpClientValidator : IClusterValidator
{
    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors)
    {
        if (cluster.HttpClient is null)
        {
            // Proxy http client options are not set.
            return ValueTask.CompletedTask;
        }

        if (cluster.HttpClient.MaxConnectionsPerServer is not null && cluster.HttpClient.MaxConnectionsPerServer <= 0)
        {
            errors.Add(new ArgumentException($"Max connections per server limit set on the cluster '{cluster.ClusterId}' must be positive."));
        }

        var requestHeaderEncoding = cluster.HttpClient.RequestHeaderEncoding;
        if (requestHeaderEncoding is not null)
        {
            try
            {
                Encoding.GetEncoding(requestHeaderEncoding);
            }
            catch (ArgumentException aex)
            {
                errors.Add(new ArgumentException($"Invalid request header encoding '{requestHeaderEncoding}'.", aex));
            }
        }

        var responseHeaderEncoding = cluster.HttpClient.ResponseHeaderEncoding;
        if (responseHeaderEncoding is null)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            Encoding.GetEncoding(responseHeaderEncoding);
        }
        catch (ArgumentException aex)
        {
            errors.Add(new ArgumentException($"Invalid response header encoding '{responseHeaderEncoding}'.", aex));
        }

        return ValueTask.CompletedTask;
    }
}
