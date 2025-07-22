// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

internal abstract class BaseEncryptedSessionAffinityPolicy<T> : ISessionAffinityPolicy
{
    private readonly IDataProtector _dataProtector;
    protected static readonly object AffinityKeyId = new object();
    protected readonly ILogger Logger;

    protected BaseEncryptedSessionAffinityPolicy(IDataProtectionProvider dataProtectionProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _dataProtector = dataProtectionProvider?.CreateProtector(GetType().FullName!) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        Logger = logger;
    }

    public abstract string Name { get; }

    public void AffinitizeResponse(HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException($"Session affinity is disabled for cluster.");
        }

        if (context.RequestAborted.IsCancellationRequested)
        {
            // Avoid wasting time if the client is already gone.
            return;
        }

        // Affinity key is set on the response only if it's a new affinity.
        if (!context.Items.ContainsKey(AffinityKeyId))
        {
            var affinityKey = GetDestinationAffinityKey(destination);
            SetAffinityKey(context, cluster, config, affinityKey);
        }
    }

    public virtual AffinityResult FindAffinitizedDestinations(HttpContext context, ClusterState cluster, SessionAffinityConfig config, IReadOnlyList<DestinationState> destinations)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException($"Session affinity is disabled for cluster {cluster.ClusterId}.");
        }

        var requestAffinityKey = GetRequestAffinityKey(context, cluster, config);

        if (requestAffinityKey.Key is null)
        {
            return new AffinityResult(null, requestAffinityKey.ExtractedSuccessfully ? AffinityStatus.AffinityKeyNotSet : AffinityStatus.AffinityKeyExtractionFailed);
        }

        IReadOnlyList<DestinationState>? matchingDestinations = null;
        if (destinations.Count > 0)
        {
            for (var i = 0; i < destinations.Count; i++)
            {
                // TODO: Add fast destination lookup by ID
                if (requestAffinityKey.Key.Equals(GetDestinationAffinityKey(destinations[i])))
                {
                    // It's allowed to affinitize a request to a pool of destinations to enable load-balancing among them.
                    // However, we currently stop after the first match found to avoid performance degradation.
                    matchingDestinations = destinations[i];
                    break;
                }
            }

            if (matchingDestinations is null)
            {
                Log.DestinationMatchingToAffinityKeyNotFound(Logger, cluster.ClusterId);
            }
        }
        else
        {
            Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(Logger, cluster.ClusterId);
        }

        // Empty destination list passed to this method is handled the same way as if no matching destinations are found.
        if (matchingDestinations is null)
        {
            return new AffinityResult(null, AffinityStatus.DestinationNotFound);
        }

        context.Items[AffinityKeyId] = requestAffinityKey;
        return new AffinityResult(matchingDestinations, AffinityStatus.OK);
    }

    protected abstract T GetDestinationAffinityKey(DestinationState destination);

    protected abstract (T? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config);

    protected abstract void SetAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config, T unencryptedKey);

    protected string Protect(string unencryptedKey)
    {
        if (string.IsNullOrEmpty(unencryptedKey))
        {
            return unencryptedKey;
        }

        var userData = Encoding.UTF8.GetBytes(unencryptedKey);

        var protectedData = _dataProtector.Protect(userData);
        return Convert.ToBase64String(protectedData).TrimEnd('=');
    }

    protected (string? Key, bool ExtractedSuccessfully) Unprotect(string? encryptedRequestKey)
    {
        if (string.IsNullOrEmpty(encryptedRequestKey))
        {
            return (Key: null, ExtractedSuccessfully: true);
        }

        try
        {
            var keyBytes = Convert.FromBase64String(Pad(encryptedRequestKey));

            var decryptedKeyBytes = _dataProtector.Unprotect(keyBytes);
            if (decryptedKeyBytes is null)
            {
                Log.RequestAffinityKeyDecryptionFailed(Logger, null);
                return (Key: null, ExtractedSuccessfully: false);
            }

            return (Key: Encoding.UTF8.GetString(decryptedKeyBytes), ExtractedSuccessfully: true);
        }
        catch (Exception ex)
        {
            Log.RequestAffinityKeyDecryptionFailed(Logger, ex);
            return (Key: null, ExtractedSuccessfully: false);
        }
    }

    private static string Pad(string text)
    {
        var padding = 3 - ((text.Length + 3) % 4);
        if (padding == 0)
        {
            return text;
        }
        return text + new string('=', padding);
    }
}
