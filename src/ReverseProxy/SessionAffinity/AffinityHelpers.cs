// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.SessionAffinity;

internal static class AffinityHelpers
{
    internal static CookieOptions CreateCookieOptions(SessionAffinityCookieConfig? config, bool isHttps, TimeProvider timeProvider)
    {
        return new CookieOptions
        {
            Path = config?.Path ?? "/",
            SameSite = config?.SameSite ?? SameSiteMode.Unspecified,
            HttpOnly = config?.HttpOnly ?? true,
            MaxAge = config?.MaxAge,
            Domain = config?.Domain,
            IsEssential = config?.IsEssential ?? false,
            Secure = config?.SecurePolicy == CookieSecurePolicy.Always || (config?.SecurePolicy == CookieSecurePolicy.SameAsRequest && isHttps),
            Expires = config?.Expiration is not null ? timeProvider.GetUtcNow().Add(config.Expiration.Value) : default(DateTimeOffset?),
        };
    }
}
