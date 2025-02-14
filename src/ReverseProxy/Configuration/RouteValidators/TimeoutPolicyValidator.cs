// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Timeouts;
#endif
using Microsoft.Extensions.Options;

namespace Yarp.ReverseProxy.Configuration.RouteValidators;

internal sealed class TimeoutPolicyValidator : IRouteValidator
{
#if NET8_0_OR_GREATER
    private readonly IOptionsMonitor<RequestTimeoutOptions> _timeoutOptions;

  public TimeoutPolicyValidator(IOptionsMonitor<RequestTimeoutOptions> timeoutOptions)
    {
        _timeoutOptions = timeoutOptions;
    }

    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors)
    {
        var timeoutPolicyName = routeConfig.TimeoutPolicy;
        var timeout = routeConfig.Timeout;

        if (!string.IsNullOrEmpty(timeoutPolicyName))
        {
            var policies = _timeoutOptions.CurrentValue.Policies;

            if (string.Equals(TimeoutPolicyConstants.Disable, timeoutPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                if (policies.TryGetValue(timeoutPolicyName, out var _))
                {
                    errors.Add(new ArgumentException($"The application has registered a timeout policy named '{timeoutPolicyName}' that conflicts with the reserved timeout policy name used on this route. The registered policy name needs to be changed for this route to function."));
                }
            }
            else if (!policies.TryGetValue(timeoutPolicyName, out var _))
            {
                errors.Add(new ArgumentException($"Timeout policy '{timeoutPolicyName}' not found for route '{routeConfig.RouteId}'."));
            }

            if (timeout.HasValue)
            {
                errors.Add(new ArgumentException($"Route '{routeConfig.RouteId}' has both a Timeout '{timeout}' and TimeoutPolicy '{timeoutPolicyName}'."));
            }
        }

        if (timeout.HasValue && timeout.Value.TotalMilliseconds <= 0)
        {
            errors.Add(new ArgumentException($"The Timeout value '{timeout.Value}' is invalid for route '{routeConfig.RouteId}'. The Timeout must be greater than zero milliseconds."));
        }

        return ValueTask.CompletedTask;
    }

#else
    public ValueTask ValidateAsync(RouteConfig routeConfig, IList<Exception> errors) => ValueTask.CompletedTask;
#endif
}
