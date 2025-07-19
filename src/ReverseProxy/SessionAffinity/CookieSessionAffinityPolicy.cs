// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class CookieSessionAffinityPolicy : BaseEncryptedSessionAffinityPolicy<string>
{
    private readonly TimeProvider _timeProvider;

    public CookieSessionAffinityPolicy(
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        ILogger<CookieSessionAffinityPolicy> logger)
        : base(dataProtectionProvider, logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public override string Name => SessionAffinityConstants.Policies.Cookie;

    protected override string GetDestinationAffinityKey(DestinationState destination)
    {
        return destination.DestinationId;
    }

    protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config)
    {
        var encryptedRequestKey = context.Request.Cookies.TryGetValue(config.AffinityKeyName, out var keyInCookie) ? keyInCookie : null;
        return Unprotect(encryptedRequestKey);
    }

    protected override void SetAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config, string unencryptedKey)
    {
        var affinityCookieOptions = AffinityHelpers.CreateCookieOptions(config.Cookie, context.Request.IsHttps, _timeProvider);

        // CodeQL [SM02373] - Whether CookieOptions.Secure is used depends on YARP configuration, and session affinity may be used in non-HTTPS setups. Cookie values are encrypted using ASP.NET DataProtection. See https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/session-affinity#key-protection.
        context.Response.Cookies.Append(config.AffinityKeyName, Protect(unencryptedKey), affinityCookieOptions);
    }
}
