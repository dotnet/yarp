// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Yarp.Kubernetes.Tests.Utils;

/// <summary>
/// Class ResourceSerializers implements the resource serializers interface.
/// Implements the <see cref="IResourceSerializers" />.
/// </summary>
/// <seealso cref="IResourceSerializers" />
public static class ResourceSerializers
{
    private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
            .WithNodeTypeResolver(new NonStringScalarTypeResolver())
            .Build();

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static T DeserializeYaml<T>(string yaml)
    {
        var resource = _yamlDeserializer.Deserialize<object>(yaml);

        return Convert<T>(resource);
    }

    public static TResource Convert<TResource>(object resource)
    {
        var json = JsonSerializer.Serialize(resource, _jsonOptions);

        return JsonSerializer.Deserialize<TResource>(json, _jsonOptions);
    }

    private class NonStringScalarTypeResolver : INodeTypeResolver
    {
        bool INodeTypeResolver.Resolve(NodeEvent nodeEvent, ref Type currentType)
        {
            if (currentType == typeof(object) && nodeEvent is Scalar)
            {
                var scalar = nodeEvent as Scalar;
                if (scalar.IsPlainImplicit)
                {
                    // TODO: should use the correct boolean parser (which accepts yes/no) instead of bool.tryparse
                    if (bool.TryParse(scalar.Value, out var _))
                    {
                        currentType = typeof(bool);
                        return true;
                    }

                    if (int.TryParse(scalar.Value, out var _))
                    {
                        currentType = typeof(int);
                        return true;
                    }

                    if (double.TryParse(scalar.Value, out var _))
                    {
                        currentType = typeof(double);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
