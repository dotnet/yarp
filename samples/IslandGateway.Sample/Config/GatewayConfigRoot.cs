﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Sample.Config
{
    internal class GatewayConfigRoot
    {
        /// <summary>
        /// Discovery mechanism used to configure Backends, Endpoints and Routes in Island Gateway.
        /// Accepted values are:
        /// <list type="bullet">
        ///   <item>
        ///     <c>static</c>, in which case <see cref="StaticDiscoveryOptions"/>
        ///     should specify the static configuration values.
        ///   </item>
        /// </list>
        /// </summary>
        public string DiscoveryMechanism { get; set; }

        /// <summary>
        /// Options that apply when <see cref="DiscoveryMechanism"/> is <c>static</c>.
        /// </summary>
        public StaticDiscoveryOptions StaticDiscoveryOptions { get; set; }
    }
}
