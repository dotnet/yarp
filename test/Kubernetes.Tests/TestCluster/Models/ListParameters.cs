// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;

namespace Yarp.Kubernetes.Tests.TestCluster.Models;

public class ListParameters
{
    [FromQuery]
    public string Continue { get; set; }

    [FromQuery]
    public string FieldSelector { get; set; }

    [FromQuery]
    public string LabelSelector { get; set; }

    [FromQuery]
    public int? Limit { get; set; }

    [FromQuery]
    public string ResourceVersion { get; set; }

    [FromQuery]
    public int? TimeoutSeconds { get; set; }

    [FromQuery]
    public bool? Watch { get; set; }

    [FromQuery]
    public string Pretty { get; set; }
}
