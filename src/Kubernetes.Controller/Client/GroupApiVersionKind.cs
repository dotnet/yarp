// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;
using System;
using System.Reflection;

namespace Yarp.Kubernetes.Controller.Client;

public struct GroupApiVersionKind : IEquatable<GroupApiVersionKind>
{
    public GroupApiVersionKind(string group, string apiVersion, string kind)
    {
        ApiVersion = apiVersion;
        GroupApiVersion = string.IsNullOrEmpty(group) ? apiVersion : $"{group}/{apiVersion}";
        Kind = kind;
    }

    public string ApiVersion { get; }

    public string GroupApiVersion { get; }

    public string Kind { get; }

    public static GroupApiVersionKind From<TResource>() => From(typeof(TResource));

    public static GroupApiVersionKind From(Type resourceType)
    {
        var entity = resourceType.GetTypeInfo().GetCustomAttribute<KubernetesEntityAttribute>();

        return new GroupApiVersionKind(
            group: entity.Group,
            apiVersion: entity.ApiVersion,
            kind: entity.Kind);
    }

    public override bool Equals(object obj)
    {
        return obj is GroupApiVersionKind kind && Equals(kind);
    }

    public bool Equals(GroupApiVersionKind other)
    {
        return GroupApiVersion == other.GroupApiVersion
            && Kind == other.Kind;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GroupApiVersion, Kind);
    }

    public override string ToString()
    {
        return $"{Kind}.{GroupApiVersion}";
    }

    public static bool operator ==(GroupApiVersionKind left, GroupApiVersionKind right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GroupApiVersionKind left, GroupApiVersionKind right)
    {
        return !(left == right);
    }
}
