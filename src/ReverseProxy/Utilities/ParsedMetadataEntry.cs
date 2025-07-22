// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class ParsedMetadataEntry<T>
{
    private readonly Parser _parser;
    private readonly string _metadataName;
    private readonly ClusterState _cluster;
    // Use a volatile field of a reference Tuple<T1, T2> type to ensure atomicity during concurrent access.
    private volatile Tuple<string?, T>? _value;

    public delegate bool Parser(string stringValue, out T parsedValue);

    public ParsedMetadataEntry(Parser parser, ClusterState cluster, string metadataName)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(metadataName);

        _parser = parser;
        _cluster = cluster;
        _metadataName = metadataName;
    }

    public T GetParsedOrDefault(T defaultValue)
    {
        var currentValue = _value;
        if (_cluster.Model.Config.Metadata is not null && _cluster.Model.Config.Metadata.TryGetValue(_metadataName, out var stringValue))
        {
            if (currentValue is null || currentValue.Item1 != stringValue)
            {
                _value = Tuple.Create<string?, T>(stringValue, _parser(stringValue, out var parsedValue) ? parsedValue : defaultValue);
            }
        }
        else if (currentValue is null || currentValue.Item1 is not null)
        {
            _value = Tuple.Create<string?, T>(null, defaultValue);
        }

        return _value!.Item2;
    }
}
