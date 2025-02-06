// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Threading.Tasks;

namespace Yarp.Sample
{
    internal sealed class TokenService
    {
        internal Task<string> GetAuthTokenAsync(ClaimsPrincipal user)
        {
            return Task.FromResult(user.Identity.Name);
        }
    }
}
