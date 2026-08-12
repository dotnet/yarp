// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

/// <summary>
/// URL rewrite rule. Maps directly to
/// <see cref="Microsoft.AspNetCore.Rewrite.RewriteOptionsExtensions.AddRewrite"/>.
/// </summary>
public sealed class RewriteRule
{
    /// <summary>The regular expression to match against the request path.</summary>
    public string? Regex { get; set; }

    /// <summary>The replacement string. May reference regex capture groups via <c>$1</c>, <c>$2</c>, etc.</summary>
    public string? Replacement { get; set; }

    /// <summary>If true (default), skip remaining rules when this rule matches.</summary>
    public bool SkipRemainingRules { get; set; } = true;
}
