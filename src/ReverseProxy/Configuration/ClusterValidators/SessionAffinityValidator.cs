// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yarp.ReverseProxy.SessionAffinity;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration.ClusterValidators;

internal sealed class SessionAffinityValidator : IClusterValidator
{
    private readonly FrozenDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;

    public SessionAffinityValidator(IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies)
    {
        ArgumentNullException.ThrowIfNull(affinityFailurePolicies);
        _affinityFailurePolicies = affinityFailurePolicies.ToDictionaryByUniqueId(p => p.Name);
    }

    public ValueTask ValidateAsync(ClusterConfig cluster, IList<Exception> errors)
    {
        if (!(cluster.SessionAffinity?.Enabled ?? false))
        {
            // Session affinity is disabled
            return ValueTask.CompletedTask;
        }

        // Note some affinity validation takes place in AffinitizeTransformProvider.ValidateCluster.
        var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
        if (string.IsNullOrEmpty(affinityFailurePolicy))
        {
            // The default.
            affinityFailurePolicy = SessionAffinityConstants.FailurePolicies.Redistribute;
        }

        if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IAffinityFailurePolicy)} found for the affinity failure policy name '{affinityFailurePolicy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (string.IsNullOrEmpty(cluster.SessionAffinity.AffinityKeyName))
        {
            errors.Add(new ArgumentException($"Affinity key name set on the cluster '{cluster.ClusterId}' must not be null."));
        }

        var cookieConfig = cluster.SessionAffinity.Cookie;

        if (cookieConfig is null)
        {
            return ValueTask.CompletedTask;
        }

        if (cookieConfig.Expiration is not null && cookieConfig.Expiration <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Session affinity cookie expiration must be positive or null."));
        }

        if (cookieConfig.MaxAge is not null && cookieConfig.MaxAge <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Session affinity cookie max-age must be positive or null."));
        }

        return ValueTask.CompletedTask;
    }
}
