// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration.ClusterValidators;
using Yarp.ReverseProxy.Configuration.RouteValidators;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Configuration;

internal sealed class ConfigValidator : IConfigValidator
{
    private readonly ITransformBuilder _transformBuilder;
    private readonly IRouteValidator[] _routeValidators;
    private readonly IClusterValidator[] _clusterValidators;

    public ConfigValidator(ITransformBuilder transformBuilder,
        IEnumerable<IRouteValidator> routeValidators,
        IEnumerable<IClusterValidator> clusterValidators)
    {
        ArgumentNullException.ThrowIfNull(transformBuilder);
        ArgumentNullException.ThrowIfNull(routeValidators);
        ArgumentNullException.ThrowIfNull(clusterValidators);

        _transformBuilder = transformBuilder;
        _routeValidators = routeValidators.ToArray();
        _clusterValidators = clusterValidators.ToArray();
    }

    // Note this performs all validation steps without short-circuiting in order to report all possible errors.
    public async ValueTask<IList<Exception>> ValidateRouteAsync(RouteConfig route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var errors = new List<Exception>();

        if (string.IsNullOrEmpty(route.RouteId))
        {
            errors.Add(new ArgumentException("Missing Route Id."));
        }

        errors.AddRange(_transformBuilder.ValidateRoute(route));

        if (route.Match is null)
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' did not set any match criteria, it requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
            return errors;
        }

        if ((route.Match.Hosts is null || route.Match.Hosts.All(string.IsNullOrEmpty)) && string.IsNullOrEmpty(route.Match.Path))
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
        }

        foreach (var routeValidator in _routeValidators)
        {
            await routeValidator.ValidateAsync(route, errors);
        }

        return errors;
    }

    // Note this performs all validation steps without short-circuiting in order to report all possible errors.
    public async ValueTask<IList<Exception>> ValidateClusterAsync(ClusterConfig cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        var errors = new List<Exception>();

        if (string.IsNullOrEmpty(cluster.ClusterId))
        {
            errors.Add(new ArgumentException("Missing Cluster Id."));
        }

        errors.AddRange(_transformBuilder.ValidateCluster(cluster));

        foreach (var clusterValidator in _clusterValidators)
        {
            await clusterValidator.ValidateAsync(cluster, errors);
        }

        return errors;
    }
}
