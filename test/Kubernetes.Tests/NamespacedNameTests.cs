// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using System.Collections.Generic;
using Xunit;

namespace Yarp.Kubernetes.Controller.Tests;

public class NamespacedNameTests
{
    [Fact]
    public void WorksAsDictionaryKey()
    {
        var dictionary = new Dictionary<NamespacedName, string>();
        var name1 = new NamespacedName("ns", "n1");
        var name2 = new NamespacedName("ns", "n2");
        var name3 = new NamespacedName("ns", "n3");

        dictionary[name1] = "one";
        dictionary[name1] = "one again";
        dictionary[name2] = "two";

        Assert.Contains(new KeyValuePair<NamespacedName, string>(name1, "one again"), dictionary);
        Assert.Contains(new KeyValuePair<NamespacedName, string>(name2, "two"), dictionary);
        Assert.DoesNotContain(name3, dictionary.Keys);
    }

    [Theory]
    [InlineData("ns", "n1", "ns", "n1", true)]
    [InlineData("ns", "n1", "ns", "n2", false)]
    [InlineData("ns", "n1", "ns-x", "n1", false)]
    [InlineData(null, "n1", null, "n1", true)]
    [InlineData(null, "n1", null, "n2", false)]
    public void EqualityAndInequality(
        string namespace1,
        string name1,
        string namespace2,
        string name2,
        bool shouldBeEqual)
    {
        var namespacedName1 = new NamespacedName(namespace1, name1);
        var namespacedName2 = new NamespacedName(namespace2, name2);

        var areEqual = namespacedName1 == namespacedName2;
        var areNotEqual = namespacedName1 != namespacedName2;
#pragma warning disable CS1718 // Comparison made to same variable
        var sameEqual1 = namespacedName1 == namespacedName1;
        var sameNotEqual1 = namespacedName1 != namespacedName1;
        var sameEqual2 = namespacedName2 == namespacedName2;
        var sameNotEqual2 = namespacedName2 != namespacedName2;
#pragma warning restore CS1718 // Comparison made to same variable

        Assert.NotEqual(areNotEqual, areEqual);
        Assert.Equal(shouldBeEqual, areEqual);
        Assert.True(sameEqual1);
        Assert.False(sameNotEqual1);
        Assert.True(sameEqual2);
        Assert.False(sameNotEqual2);
    }

    [Fact]
    public void NamespaceAndNameFromResource()
    {
        var resource = new V1ConfigMap
        {
            ApiVersion = V1ConfigMap.KubeApiVersion,
            Kind = V1ConfigMap.KubeKind,
            Metadata = new V1ObjectMeta {Name = "the-name", NamespaceProperty = "the-namespace"}
        };

        var nn = NamespacedName.From(resource);

        Assert.Equal("the-name", nn.Name);
        Assert.Equal("the-namespace", nn.Namespace);
    }

    [Fact]
    public void JustNameFromClusterResource()
    {
        var resource = new V1ClusterRole
        {
            ApiVersion = V1ClusterRole.KubeApiVersion,
            Kind = V1ClusterRole.KubeKind,
            Metadata = new V1ObjectMeta { Name = "the-name" }
        };

        var nn = NamespacedName.From(resource);

        Assert.Equal("the-name", nn.Name);
        Assert.Null(nn.Namespace);
    }
}
