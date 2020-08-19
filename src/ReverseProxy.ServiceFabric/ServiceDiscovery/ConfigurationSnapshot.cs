// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    // TODO: davidni: This shouldn't be needed, perhaps YARP should provide it...
    internal class ConfigurationSnapshot : IProxyConfig
    {
        public IReadOnlyList<ProxyRoute> Routes { get; internal set; }

        public IReadOnlyList<Cluster> Clusters { get; internal set; }

        public IChangeToken ChangeToken { get; internal set; }
    }
}
