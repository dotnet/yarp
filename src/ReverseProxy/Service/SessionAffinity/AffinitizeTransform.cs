// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Affinitizes request to a chosen <see cref="DestinationInfo"/>.
    /// </summary>
    internal class AffinitizeTransform : ResponseTransform
    {
        private readonly ISessionAffinityProvider _sessionAffinityProvider;

        public AffinitizeTransform(ISessionAffinityProvider sessionAffinityProvider)
        {
            _sessionAffinityProvider = sessionAffinityProvider ?? throw new ArgumentNullException(nameof(sessionAffinityProvider));
        }

        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            var proxyFeature = context.HttpContext.GetRequiredProxyFeature();
            var options = proxyFeature.ClusterConfig.Options.SessionAffinity;
            var selectedDestination = proxyFeature.SelectedDestination;
            _sessionAffinityProvider.AffinitizeRequest(context.HttpContext, options, selectedDestination);
            return default;
        }
    }
}
