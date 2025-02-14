// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Yarp.Kubernetes.Tests.TestCluster.Models;

public class ListResult
{
    public string Continue { get; set; }

    public string ResourceVersion { get; set; }

    public IList<ResourceObject> Items { get; set; }
}
