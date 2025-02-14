// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Yarp.Sample
{
    internal sealed class TokenService
    {
        internal Task<string> GetAuthTokenAsync(ClaimsPrincipal user)
        {
            // we only have tokens for bob
            if (string.Equals("Bob", user.Identity.Name))
            {
                return Task.FromResult(Guid.NewGuid().ToString());
            }
            return Task.FromResult<string>(null);
        }
    }
}
