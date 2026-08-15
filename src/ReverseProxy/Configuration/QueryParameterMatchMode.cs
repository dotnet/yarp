// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// How to match Query Parameter values.
/// </summary>
public enum QueryParameterMatchMode
{
    /// <summary>
    /// Any of the values for the given query parameter must match in its entirety, subject to case sensitivity settings.
    /// </summary>
    Exact,

    /// <summary>
    /// Any of the values for the given query parameter must contain any of the match values, subject to case sensitivity settings.
    /// </summary>
    Contains,

    /// <summary>
    /// None of the values for the given query parameter may contain any of the match values, subject to case sensitivity settings.
    /// The rule also matches if the query parameter is missing or its value is empty.
    /// </summary>
    NotContains,

    /// <summary>
    /// Any of the values for the given query parameter must match by prefix, subject to case sensitivity settings.
    /// </summary>
    Prefix,

    /// <summary>
    /// The query parameter must exist and contain any non-empty value.
    /// If the query string contains multiple values for that name, the rule will also match.
    /// </summary>
    Exists
}
