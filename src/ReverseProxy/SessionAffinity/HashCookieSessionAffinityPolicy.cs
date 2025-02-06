// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class HashCookieSessionAffinityPolicy : BaseHashCookieSessionAffinityPolicy
{
    private readonly ConditionalWeakTable<DestinationState, string> _hashes = new();

    public HashCookieSessionAffinityPolicy(
        TimeProvider timeProvider,
        ILogger<HashCookieSessionAffinityPolicy> logger)
        : base(timeProvider, logger) { }

    public override string Name => SessionAffinityConstants.Policies.HashCookie;

    protected override string GetDestinationHash(DestinationState d)
    {
        return _hashes.GetValue(d, static d =>
        {
            // Stable format across instances
            var destinationIdBytes = Encoding.Unicode.GetBytes(d.DestinationId.ToUpperInvariant());
            var hashBytes = XxHash64.Hash(destinationIdBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        });
    }
}
