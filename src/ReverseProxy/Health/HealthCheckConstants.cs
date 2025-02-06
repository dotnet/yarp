// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Health;

public static class HealthCheckConstants
{
    public static class PassivePolicy
    {
        public static readonly string TransportFailureRate = nameof(TransportFailureRate);
    }

    public static class ActivePolicy
    {
        public static readonly string ConsecutiveFailures = nameof(ConsecutiveFailures);
    }

    public static class AvailableDestinations
    {
        /// <summary>
        /// Marks destination as available for proxying requests to if its health state
        /// is either 'Healthy' or 'Unknown'. If no destinations are available then
        /// requests will get a 503 error.
        /// </summary>
        /// <remarks>It applies only if active or passive health checks are enabled.</remarks>
        public static readonly string HealthyAndUnknown = nameof(HealthyAndUnknown);

        /// <summary>
        /// Calls <see cref="HealthyAndUnknown"/> policy at first to determine
        /// destinations' availability. If no available destinations are returned
        /// from this call, it marks all cluster's destination as available.
        /// </summary>
        public static readonly string HealthyOrPanic = nameof(HealthyOrPanic);
    }
}
