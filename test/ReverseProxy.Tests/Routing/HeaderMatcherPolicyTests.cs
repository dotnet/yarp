// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Routing.Tests;

public class HeaderMatcherPolicyTests
{
    [Fact]
    public void Comparer_SortOrder_SingleRuleEqual()
    {
        // Most specific to least
        var endpoints = new[]
        {
            (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)),

            (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.ExactHeader)),
            (0, CreateEndpoint("header", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),
            (0, CreateEndpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.ExactHeader)),

            (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, isCaseSensitive: true)),

            (0, CreateEndpoint("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix)),
            (0, CreateEndpoint("header", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),
            (0, CreateEndpoint("header2", new[] { "abc", "def" }, HeaderMatchMode.HeaderPrefix)),

            (0, CreateEndpoint("header", Array.Empty<string>(), HeaderMatchMode.Exists, isCaseSensitive: true)),
            (0, CreateEndpoint("header", Array.Empty<string>(), HeaderMatchMode.Exists)),
            (0, CreateEndpoint("header", Array.Empty<string>(), HeaderMatchMode.Exists, isCaseSensitive: true)),
            (0, CreateEndpoint("header", Array.Empty<string>(), HeaderMatchMode.Exists)),
        };
        var sut = new HeaderMatcherPolicy();

        for (var i = 0; i < endpoints.Length; i++)
        {
            for (var j = 0; j < endpoints.Length; j++)
            {
                var a = endpoints[i];
                var b = endpoints[j];

                var actual = sut.Comparer.Compare(a.Item2, b.Item2);
                var expected =
                    a.Item1 < b.Item1 ? -1 :
                    a.Item1 > b.Item1 ? 1 : 0;
                if (actual != expected)
                {
                    Assert.Fail($"Error comparing [{i}] to [{j}], expected {expected}, found {actual}.");
                }
            }
        }
    }

    [Fact]
    public void Comparer_MultipleHeaders_SortOrder()
    {
        // Most specific to least
        var endpoints = new[]
        {
            (0, CreateEndpoint(new[]
            {
                new HeaderMatcher("header", Array.Empty<string>(), HeaderMatchMode.Exists, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.HeaderPrefix, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "cbcabc" }, HeaderMatchMode.Contains, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
            })),

            (1, CreateEndpoint(new[]
            {
                new HeaderMatcher("header", new[] { "cbcabc" }, HeaderMatchMode.Contains, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
            })),
            (1, CreateEndpoint(new[]
            {
                new HeaderMatcher("header", Array.Empty<string>(), HeaderMatchMode.Exists, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
            })),

            (2, CreateEndpoint("header", new[] { "abc" })),

            (3, CreateEndpoint(Array.Empty<HeaderMatcher>())),
        };
        var sut = new HeaderMatcherPolicy();

        for (var i = 0; i < endpoints.Length; i++)
        {
            for (var j = 0; j < endpoints.Length; j++)
            {
                var a = endpoints[i];
                var b = endpoints[j];

                var actual = sut.Comparer.Compare(a.Item2, b.Item2);
                var expected =
                    a.Item1 < b.Item1 ? -1 :
                    a.Item1 > b.Item1 ? 1 : 0;
                if (actual != expected)
                {
                    Assert.Fail($"Error comparing [{i}] to [{j}], expected {expected}, found {actual}.");
                }
            }
        }
    }

    [Fact]
    public void AppliesToEndpoints_AppliesScenarios()
    {
        var scenarios = new[]
        {
            CreateEndpoint("org-id", Array.Empty<string>(), HeaderMatchMode.Exists),
            CreateEndpoint("org-id", new[] { "abc" }),
            CreateEndpoint("org-id", new[] { "abc", "def" }),
            CreateEndpoint("org-id", Array.Empty<string>(), HeaderMatchMode.Exists, isDynamic: true),
            CreateEndpoint("org-id", new[] { "abc" }, isDynamic: true),
            CreateEndpoint("org-id", null, HeaderMatchMode.Exists, isDynamic: true),
            CreateEndpoint(new[]
            {
                new HeaderMatcher("header", Array.Empty<string>(), HeaderMatchMode.Exists, isCaseSensitive: true),
                new HeaderMatcher("header", new[] { "abc" }, HeaderMatchMode.ExactHeader, isCaseSensitive: true)
            })
        };
        var sut = new HeaderMatcherPolicy();
        var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

        for (var i = 0; i < scenarios.Length; i++)
        {
            var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { scenarios[i] });
            Assert.True(result, $"scenario {i}");
        }
    }

    [Fact]
    public void AppliesToEndpoints_NoMetadata_DoesNotApply()
    {
        var endpoint = CreateEndpoint(Array.Empty<HeaderMatcher>());

        var sut = new HeaderMatcherPolicy();
        var endpointSelectorPolicy = (IEndpointSelectorPolicy)sut;

        var result = endpointSelectorPolicy.AppliesToEndpoints(new[] { endpoint });
        Assert.False(result);
    }

    [Theory]
    [InlineData(null, HeaderMatchMode.Exists, false)]
    [InlineData("", HeaderMatchMode.Exists, false)]
    [InlineData("abc", HeaderMatchMode.Exists, true)]
    [InlineData(null, HeaderMatchMode.NotExists, true)]
    [InlineData("", HeaderMatchMode.NotExists, true)]
    [InlineData("abc", HeaderMatchMode.NotExists, false)]
    public async Task ApplyAsync_MatchingScenarios_AnyHeaderValue(string incomingHeaderValue, HeaderMatchMode mode, bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        if (incomingHeaderValue is not null)
        {
            context.Request.Headers["org-id"] = incomingHeaderValue;
        }

        var endpoint = CreateEndpoint("org-id", Array.Empty<string>(), mode);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, null, false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "aBC", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abcd", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "aBC", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abcd", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "ab", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, ";", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab;c", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, ";abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, ";abC", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc;", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "Abc;", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc;def", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ABC;DEF", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "def;abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc;aBc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "def;ab c", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "bcd;efg", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"abc", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "abc\"", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, " \"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"abc\" ", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"abc\", ", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"ab\", \"abc", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"ab\", \"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab\", \"abc", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab\"\",\"abc", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "ab\"\",\"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"\"ab\"\"c", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"\"ab\"\"c,\"abc,\"", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, false, "\"\"ab\"\"c,\"abc,\",abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, ";", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "ab;c", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, ";abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abc;", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abc;def", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "abc;abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "def;abc", true)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "def;abC", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "def;ab c", false)]
    [InlineData("abc", HeaderMatchMode.ExactHeader, true, "bcd;efg", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "aBC", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abcd", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "ab", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "aBC", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abcd", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "aBCd", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, ";", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abc;", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, ";aBc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abd;abC", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, false, "abd;abe", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abc;", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, ";abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abd;abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "abd;abe", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab\"c", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab\"c\"", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"abc", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, " \"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, " \"abc", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"abc\" ", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab,abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "ab, abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"ab, abc\"", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"ab\", abc", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"ab\", abc\"", true)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"ab\"\"\"\", abc\"", false)]
    [InlineData("abc", HeaderMatchMode.HeaderPrefix, true, "\"ab\"\"\"\"\", abc\"", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "", false)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ababc", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "zaBCz", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "dcbaabcd", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ababab", false)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "", false)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "abcc", true)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "aaaBC", false)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "bbabcdb", true)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "aBCcba", false)]
    [InlineData("abc", HeaderMatchMode.Contains, true, "baab", false)]
    [InlineData("abc", HeaderMatchMode.Contains, false, ";", false)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ababc;", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, ";ababc", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ab;cd", false)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ab;cd;abcd", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "abc;abc;def", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "\"abc", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "abc\"", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "\"abc\"", true)]
    [InlineData("abc", HeaderMatchMode.Contains, false, "ab\"c", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, null, true)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "ababc", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "zaBCz", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "dcbaabcd", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "ababab", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, null, true)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "abcc", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "aaaBC", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "bbabcdb", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "aBCcba", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, true, "baab", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, ";abc", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "abc;", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "ab;cd", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "ababc;abc", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "abc;def", false)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "ab\"c", true)]
    [InlineData("abc", HeaderMatchMode.NotContains, false, "\"abc\"", false)]
    public async Task ApplyAsync_MatchingScenarios_OneHeaderValue(
        string headerValue,
        HeaderMatchMode headerValueMatchMode,
        bool isCaseSensitive,
        string incomingHeaderValues,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        if (incomingHeaderValues is not null)
        {
            context.Request.Headers["org-id"] = incomingHeaderValues.Split(';');
        }

        var endpoint = CreateEndpoint("org-id", new[] { headerValue }, headerValueMatchMode, isCaseSensitive);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "abc", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "aBc", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "abcd", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "def", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "deF", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, false, "defg", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "abc", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "aBC", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "aBCd", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "def", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "DEFg", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "dEf", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, ";", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "abc;a", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "a;abc", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "abc;def", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "ab;def", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "ab;cdef", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "ab;\"def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "\"abc,def\"", false)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, "\"abc\",def", true)]
    [InlineData("abc", "def", HeaderMatchMode.ExactHeader, true, " \"abc\",def", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abc", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "aBc", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abcd", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "abcD", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "def", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "deF", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "defg", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "defG", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, false, "aabc", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "abc", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aBC", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aBCd", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "def", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "DEFg", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "aabc", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, ";", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "ab;cde;fgh", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "abcd;e", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "abcd;defg", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "Abcd;defg", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "Abcd;Defg", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "a;defg", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "abcd;", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, ";def", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, " \"abc\",def", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "ab, \"def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "ab, def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "ab, \"def", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "\"\"ab\",def", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "\"\"ab\",def\"", false)]
    [InlineData("abc", "def", HeaderMatchMode.HeaderPrefix, true, "\"\"ab\"\",def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "aabc", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "baBc", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "ababcd", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "dcabcD", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "fdeff", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "edeF", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "adefg", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "abdefG", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "ddaabc", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, false, "abcdef", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, null, false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "cabca", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "aBCa", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "CaBCdd", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "DEFdef", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "defDEFg", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "bbaabc", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, ";", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "cabca;", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, ";cabca", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "ab;cd;ef", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "aBCa;deFg", false)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "aBCa;defg", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "abcd;d", true)]
    [InlineData("abc", "ABC", HeaderMatchMode.Contains, true, "abc;d", true)]
    [InlineData("abc", "ABC", HeaderMatchMode.Contains, true, "ABC;d", true)]
    [InlineData("abc", "ABC", HeaderMatchMode.Contains, true, "abC;d", false)]
    [InlineData("abc", "ABC", HeaderMatchMode.Contains, true, "abcABC;d", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "\"abc, def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "\"abc\", def\"", true)]
    [InlineData("abc", "def", HeaderMatchMode.Contains, true, "ab\"cde\"f", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, null, true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "aabc", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "baBc", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "ababcd", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "dcabcD", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "def", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, false, "ghi", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, null, true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "cabca", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "aBCa", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "CaBCdd", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "DEFdef", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "DEFg", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "bbaabc", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "defG", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "bbaabc;", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, ";bbaabc", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "ab;cd;ef", true)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "a;defg", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "ab;cdef", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "abc;def", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "Abc;cdef", false)]
    [InlineData("abc", "def", HeaderMatchMode.NotContains, true, "Abc;cdEf", true)]
    public async Task ApplyAsync_MatchingScenarios_TwoHeaderValues(
        string header1Value,
        string header2Value,
        HeaderMatchMode headerValueMatchMode,
        bool isCaseSensitive,
        string incomingHeaderValues,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        if (incomingHeaderValues is not null)
        {
            context.Request.Headers["org-id"] = incomingHeaderValues.Split(';');
        }

        var endpoint = CreateEndpoint("org-id", new[] { header1Value, header2Value }, headerValueMatchMode, isCaseSensitive);

        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData(HeaderMatchMode.Contains, true, false)]
    [InlineData(HeaderMatchMode.Contains, false, false)]
    [InlineData(HeaderMatchMode.NotContains, true, true)]
    [InlineData(HeaderMatchMode.NotContains, false, true)]
    [InlineData(HeaderMatchMode.HeaderPrefix, true, false)]
    [InlineData(HeaderMatchMode.HeaderPrefix, false, false)]
    [InlineData(HeaderMatchMode.ExactHeader, true, false)]
    [InlineData(HeaderMatchMode.ExactHeader, false, false)]
    [InlineData(HeaderMatchMode.NotExists, true, true)]
    [InlineData(HeaderMatchMode.NotExists, false, true)]
    [InlineData(HeaderMatchMode.Exists, true, false)]
    [InlineData(HeaderMatchMode.Exists, false, false)]
    public async Task ApplyAsync_MatchingScenarios_MissingHeader(
        HeaderMatchMode headerValueMatchMode,
        bool isCaseSensitive,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();

        var headerValues = new[] { "bar" };
        if (headerValueMatchMode == HeaderMatchMode.Exists
            || headerValueMatchMode == HeaderMatchMode.NotExists)
        {
            headerValues = null;
        }

        var endpoint = CreateEndpoint("foo", headerValues, headerValueMatchMode, isCaseSensitive);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData("Foo", "abc", HeaderMatchMode.ExactHeader, "ab, abc", true)]
    [InlineData("Foo", "abc", HeaderMatchMode.ExactHeader, "ab; abc", false)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "ab, abc", false)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "ab; abc", true)]
    [InlineData("Set-Cookie", "abc", HeaderMatchMode.ExactHeader, "ab, abc", true)]
    [InlineData("Set-Cookie", "abc", HeaderMatchMode.ExactHeader, "ab; abc", false)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "\"ab\"; abc", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "ab; \"abc\"", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "\"ab\"; \"abc\"", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "abc;", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, " abc;", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, " \"abc\";", true)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "\"abc;\"", false)]
    [InlineData("Cookie", "abc", HeaderMatchMode.ExactHeader, "\"abc;\" \"abc\"", false)]
    public async Task ApplyAsync_Cookie_UsesDifferentSeparator(
        string headerName,
        string headerValue,
        HeaderMatchMode headerValueMatchMode,
        string incomingHeaderValue,
        bool shouldMatch)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[headerName] = incomingHeaderValue;

        var endpoint = CreateEndpoint(headerName, new[] { headerValue }, headerValueMatchMode, true);
        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ApplyAsync_MultipleRules_RequiresAllHeaders(bool sendHeader1, bool sendHeader2, bool shouldMatch)
    {
        var endpoint = CreateEndpoint(new[]
        {
            new HeaderMatcher("header1", new[] { "value1" }, HeaderMatchMode.ExactHeader, isCaseSensitive: false),
            new HeaderMatcher("header2", new[] { "value2" }, HeaderMatchMode.ExactHeader, isCaseSensitive: false)
        });

        var context = new DefaultHttpContext();
        if (sendHeader1)
        {
            context.Request.Headers["header1"] = "value1";
        }
        if (sendHeader2)
        {
            context.Request.Headers["header2"] = "value2";
        }

        var candidates = new CandidateSet(new[] { endpoint }, new RouteValueDictionary[1], new int[1]);
        var sut = new HeaderMatcherPolicy();

        await sut.ApplyAsync(context, candidates);

        Assert.Equal(shouldMatch, candidates.IsValidCandidate(0));
    }

    private static Endpoint CreateEndpoint(
        string headerName,
        string[] headerValues,
        HeaderMatchMode mode = HeaderMatchMode.ExactHeader,
        bool isCaseSensitive = false,
        bool isDynamic = false)
    {
        return CreateEndpoint(new[] { new HeaderMatcher(headerName, headerValues, mode, isCaseSensitive) }, isDynamic);
    }

    private static Endpoint CreateEndpoint(IReadOnlyList<HeaderMatcher> matchers, bool isDynamic = false)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0);
        builder.Metadata.Add(new HeaderMetadata(matchers));
        if (isDynamic)
        {
            builder.Metadata.Add(new DynamicEndpointMetadata());
        }

        return builder.Build();
    }

    private class DynamicEndpointMetadata : IDynamicEndpointMetadata
    {
        public bool IsDynamic => true;
    }
}
