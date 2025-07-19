// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using k8s.Models;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Yarp.Kubernetes.Controller;

/// <summary>
/// Struct NamespacedName is a value that acts as a dictionary key. It is a comparable
/// combination of a metadata namespace and name.
/// Implements the <see cref="IEquatable{T}" />.
/// </summary>
/// <seealso cref="IEquatable{T}" />
public struct NamespacedName : IEquatable<NamespacedName>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NamespacedName"/> struct.
    /// </summary>
    /// <param name="namespace">The namespace.</param>
    /// <param name="name">The name.</param>
    [JsonConstructor]
    public NamespacedName(string @namespace, string name)
    {
        Namespace = @namespace;
        Name = name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamespacedName"/> struct.
    /// </summary>
    /// <param name="name">The name.</param>
    public NamespacedName(string name)
    {
        Namespace = null;
        Name = name;
    }

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    /// <value>The namespace.</value>
    public string Namespace { get; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; }

    /// <summary>
    /// Implements the == operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator ==(NamespacedName left, NamespacedName right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Implements the != operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator !=(NamespacedName left, NamespacedName right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Gets key values from the specified resource.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>NamespacedName.</returns>
    public static NamespacedName From(IKubernetesObject<V1ObjectMeta> resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return new NamespacedName(resource.Namespace(), resource.Name());
    }

    /// <summary>
    /// Gets key values from the specified metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="ownerReference">The owner reference.</param>
    /// <param name="clusterScoped">if set to <c>true</c> [cluster scoped].</param>
    /// <returns>NamespacedName.</returns>
    public static NamespacedName From(V1ObjectMeta metadata, [NotNull] V1OwnerReference ownerReference, bool? clusterScoped = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(ownerReference);

        return new NamespacedName(
            clusterScoped ?? false ? null : metadata.NamespaceProperty,
            ownerReference.Name);
    }

    public override bool Equals(object obj)
    {
        return obj is NamespacedName name && Equals(name);
    }

    public bool Equals([AllowNull] NamespacedName other)
    {
        return Namespace == other.Namespace && Name == other.Name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Namespace, Name);
    }

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="string" /> that represents this instance.</returns>
    public override string ToString()
    {
        return $"{Namespace}/{Name}";
    }
}
