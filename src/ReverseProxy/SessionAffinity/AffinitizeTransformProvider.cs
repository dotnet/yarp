// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class AffinitizeTransformProvider : ITransformProvider
{
    private readonly FrozenDictionary<string, ISessionAffinityPolicy> _sessionAffinityPolicies;

    public AffinitizeTransformProvider(IEnumerable<ISessionAffinityPolicy> sessionAffinityPolicies)
    {
        ArgumentNullException.ThrowIfNull(sessionAffinityPolicies);
        _sessionAffinityPolicies = sessionAffinityPolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        // Other affinity validation logic is covered by ConfigValidator.ValidateSessionAffinity.
        if (!(context.Cluster.SessionAffinity?.Enabled ?? false))
        {
            return;
        }

        var policy = context.Cluster.SessionAffinity.Policy;
        if (string.IsNullOrEmpty(policy))
        {
            // The default.
            policy = SessionAffinityConstants.Policies.HashCookie;
        }

        if (!_sessionAffinityPolicies.ContainsKey(policy))
        {
            context.Errors.Add(new ArgumentException($"No matching {nameof(ISessionAffinityPolicy)} found for the session affinity policy '{policy}' set on the cluster '{context.Cluster.ClusterId}'."));
        }
    }

    public void Apply(TransformBuilderContext context)
    {
        var options = context.Cluster?.SessionAffinity;

        if (options is not null && options.Enabled.GetValueOrDefault())
        {
            var policy = _sessionAffinityPolicies.GetRequiredServiceById(options.Policy, SessionAffinityConstants.Policies.HashCookie);
            context.ResponseTransforms.Add(new AffinitizeTransform(policy));
        }
    }
}
