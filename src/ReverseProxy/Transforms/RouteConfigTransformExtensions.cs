// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Extensions for adding transforms to <see cref="RouteConfig"/>.
/// </summary>
public static class RouteConfigTransformExtensions
{
    /// <summary>
    /// Clones the <see cref="RouteConfig"/> and adds the transform.
    /// </summary>
    /// <returns>The cloned route with the new transform.</returns>
    public static RouteConfig WithTransform(this RouteConfig route, Action<IDictionary<string, string>> createTransform)
    {
        ArgumentNullException.ThrowIfNull(createTransform);

        List<IReadOnlyDictionary<string, string>> transforms;
        if (route.Transforms is null)
        {
            transforms = new List<IReadOnlyDictionary<string, string>>();
        }
        else
        {
            transforms = new List<IReadOnlyDictionary<string, string>>(route.Transforms.Count + 1);
            transforms.AddRange(route.Transforms);
        }

        var transform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        createTransform(transform);
        transforms.Add(transform);

        return route with { Transforms = transforms };
    }
}
