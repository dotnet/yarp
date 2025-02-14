// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// How to match header values.
/// </summary>
public enum HeaderMatchMode
{
    /// <summary>
    /// Any of the headers with the given name must match in its entirety, subject to case sensitivity settings.
    /// If a header contains multiple values (separated by , or ;), they are split before matching.
    /// A single pair of quotes will also be stripped from the value before matching.
    /// </summary>
    ExactHeader,

    /// <summary>
    /// Any of the headers with the given name must match by prefix, subject to case sensitivity settings.
    /// If a header contains multiple values (separated by , or ;), they are split before matching.
    /// A single pair of quotes will also be stripped from the value before matching.
    /// </summary>
    HeaderPrefix,

    /// <summary>
    /// Any of the headers with the given name must contain any of the match values, subject to case sensitivity settings.
    /// </summary>
    Contains,

    /// <summary>
    /// The header must exist and the value must be non-empty.
    /// None of the headers with the given name may contain any of the match values, subject to case sensitivity settings.
    /// </summary>
    NotContains,

    /// <summary>
    /// The header must exist and contain any non-empty value.
    /// If there are multiple headers with the same name, the rule will also match.
    /// </summary>
    Exists,

    /// <summary>
    /// The header must not exist.
    /// </summary>
    NotExists,
}
