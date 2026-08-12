// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Creates a query parameter value by substituting template tokens from route or query values.
/// </summary>
public sealed class QueryParameterTemplateTransform : QueryParameterTransform
{
    private readonly TemplateSegment[] _segments;

    public QueryParameterTemplateTransform(QueryStringTransformMode mode, string key, string template)
        : base(mode, key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        ArgumentNullException.ThrowIfNull(template);

        Template = template;
        _segments = TemplateParser.Parse(template);
    }

    internal string Template { get; }

    /// <inheritdoc/>
    protected override string? GetValue(RequestTransformContext context)
    {
        var builder = new StringBuilder();
        foreach (var segment in _segments)
        {
            if (!segment.IsToken)
            {
                builder.Append(segment.Value);
                continue;
            }

            if (!TryResolveToken(context, segment.Value, out var tokenValue))
            {
                return null;
            }

            builder.Append(tokenValue);
        }

        return builder.ToString();
    }

    private static bool TryResolveToken(RequestTransformContext context, string tokenName, out string? value)
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        if (routeValues.TryGetValue(tokenName, out var routeValue) && routeValue is not null)
        {
            value = routeValue.ToString();
            return true;
        }

        if (context.Query.Collection.TryGetValue(tokenName, out var queryValue))
        {
            value = queryValue.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private readonly record struct TemplateSegment(bool IsToken, string Value);

    private static class TemplateParser
    {
        public static TemplateSegment[] Parse(string template)
        {
            var segments = new List<TemplateSegment>();
            var index = 0;
            while (index < template.Length)
            {
                var openIndex = template.IndexOf('{', index);
                if (openIndex < 0)
                {
                    if (index < template.Length)
                    {
                        segments.Add(new TemplateSegment(false, template.Substring(index)));
                    }
                    break;
                }

                if (openIndex > index)
                {
                    segments.Add(new TemplateSegment(false, template.Substring(index, openIndex - index)));
                }

                var closeIndex = template.IndexOf('}', openIndex + 1);
                if (closeIndex < 0)
                {
                    segments.Add(new TemplateSegment(false, template.Substring(openIndex)));
                    break;
                }

                var rawToken = template.Substring(openIndex + 1, closeIndex - openIndex - 1);
                var tokenName = NormalizeTokenName(rawToken);
                if (string.IsNullOrEmpty(tokenName))
                {
                    segments.Add(new TemplateSegment(false, template.Substring(openIndex, closeIndex - openIndex + 1)));
                }
                else
                {
                    segments.Add(new TemplateSegment(true, tokenName));
                }

                index = closeIndex + 1;
            }

            return segments.ToArray();
        }

        private static string? NormalizeTokenName(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            token = token.Trim();

            while (token.Length > 0 && token[0] == '*')
            {
                token = token.Substring(1);
            }

            var constraintIndex = token.IndexOf(':', StringComparison.Ordinal);
            if (constraintIndex >= 0)
            {
                token = token.Substring(0, constraintIndex);
            }

            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }
}
