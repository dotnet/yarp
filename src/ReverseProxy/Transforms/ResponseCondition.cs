// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Specifies the conditions under which a response transform will run.
/// </summary>
public enum ResponseCondition
{
    /// <summary>
    /// The transform runs for all conditions.
    /// </summary>
    Always,

    /// <summary>
    /// The transform only runs if there is a successful response with a status code less than 400.
    /// </summary>
    Success,

    /// <summary>
    /// The transform only runs if there is no response or a response with a 400+ status code.
    /// </summary>
    Failure
}
