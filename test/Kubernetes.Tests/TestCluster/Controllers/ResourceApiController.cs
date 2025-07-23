// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Yarp.Kubernetes.Tests.TestCluster.Models;

namespace Yarp.Kubernetes.Tests.TestCluster;

[Route("api/{version}/{plural}")]
public class ResourceApiController : ControllerBase
{
    private readonly ITestCluster _testCluster;

    public ResourceApiController(ITestCluster testCluster)
    {
        _testCluster = testCluster;
    }

    [FromRoute]
    public string Version { get; set; }

    [FromRoute]
    public string Plural { get; set; }

    [HttpGet]
    public async Task<IActionResult> ListAsync(ListParameters parameters)
    {
        var list = await _testCluster.ListResourcesAsync(string.Empty, Version, Plural, parameters);

        var result = new KubernetesList<ResourceObject>(
            apiVersion: Version,
            kind: "PodList",
            metadata: new V1ListMeta(
                continueProperty: list.Continue,
                remainingItemCount: null,
                resourceVersion: list.ResourceVersion),
            items: list.Items);

        return new ObjectResult(result);
    }
}
