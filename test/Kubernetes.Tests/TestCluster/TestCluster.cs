// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yarp.Kubernetes.Tests.TestCluster.Models;
using Yarp.Kubernetes.Tests.Utils;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestCluster : ITestCluster
{
    public IList<ResourceObject> Resources { get; } = new List<ResourceObject>();

    public TestCluster(IOptions<TestClusterOptions> options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        foreach (var resource in options.Value.InitialResources)
        {
            Resources.Add(ResourceSerializers.Convert<ResourceObject>(resource));
        }
    }

    public virtual Task UnhandledRequest(HttpContext context)
    {
        throw new NotImplementedException();
    }

    public virtual Task<ListResult> ListResourcesAsync(string group, string version, string plural, ListParameters parameters)
    {
        if (string.IsNullOrEmpty(version))
        {
            throw new ArgumentException($"'{nameof(version)}' cannot be null or empty", nameof(version));
        }

        if (string.IsNullOrEmpty(plural))
        {
            throw new ArgumentException($"'{nameof(plural)}' cannot be null or empty", nameof(plural));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        return Task.FromResult(new ListResult
        {
            ResourceVersion = parameters.ResourceVersion,
            Continue = null,
            Items = Resources.ToArray(),
        });
    }
}
