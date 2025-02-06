// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// How to match Query Parameter values.
/// </summary>
public enum QueryParameterMatchMode
{
    /// <summary>
    /// Query string must match in its entirety,
    /// Subject to case sensitivity settings.
    /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
    /// </summary>
    Exact,

    /// <summary>
    /// Query string key must be present and substring must match for each of the respective query string values.
    /// Subject to case sensitivity settings.
    /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
    /// </summary>
    Contains,

    /// <summary>
    /// Query string key must be present and value must not match for each of the respective query string values.
    /// Subject to case sensitivity settings.
    /// If there are multiple values then it needs to not contain ANY of the values 
    /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
    /// </summary>
    NotContains,

    /// <summary>
    /// Query string key must be present and prefix must match for each of the respective query string values.
    /// Subject to case sensitivity settings.
    /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
    /// </summary>
    Prefix,

    /// <summary>
    /// Query string key must exist and contain any non-empty value.
    /// </summary>
    Exists
}
