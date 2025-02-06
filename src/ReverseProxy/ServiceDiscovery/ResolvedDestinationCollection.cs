// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceDiscovery
{
    /// <summary>
    /// Represents a collection of resolved destinations.
    /// </summary>
    public sealed class ResolvedDestinationCollection
    {
        public ResolvedDestinationCollection(IReadOnlyDictionary<string, DestinationConfig> destinations, IChangeToken? changeToken)
        {
            Destinations = destinations;
            ChangeToken = changeToken;
        }

        /// <summary>
        /// Gets the map of destination names to destination configurations.
        /// </summary>
        public IReadOnlyDictionary<string, DestinationConfig> Destinations { get; init; }

        /// <summary>
        /// Gets the optional change token used to signal when this collection should be refreshed.
        /// </summary>
        public IChangeToken? ChangeToken { get; init; }
    }
}
