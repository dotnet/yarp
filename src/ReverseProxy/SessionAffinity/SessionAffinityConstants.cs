// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.SessionAffinity;

/// <summary>
/// Names of built-in session affinity services.
/// </summary>
public static class SessionAffinityConstants
{
    public static class Policies
    {
        public static string Cookie => nameof(Cookie);

        public static string HashCookie => nameof(HashCookie);

        public static string ArrCookie => nameof(ArrCookie);

        public static string CustomHeader => nameof(CustomHeader);
    }

    public static class FailurePolicies
    {
        public static string Redistribute => nameof(Redistribute);

        public static string Return503Error => nameof(Return503Error);
    }
}
