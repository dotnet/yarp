// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class RequestUtilitiesTests
{
    [Fact]
    public void GetHttpMethod_Get_Works()
    {
        Assert.Same(HttpMethod.Get, RequestUtilities.GetHttpMethod("GET"));
    }

    [Fact]
    public void GetHttpMethod_Post_Works()
    {
        Assert.Same(HttpMethod.Post, RequestUtilities.GetHttpMethod("POST"));
    }

    [Fact]
    public void GetHttpMethod_Put_Works()
    {
        Assert.Same(HttpMethod.Put, RequestUtilities.GetHttpMethod("PUT"));
    }

    [Fact]
    public void GetHttpMethod_Delete_Works()
    {
        Assert.Same(HttpMethod.Delete, RequestUtilities.GetHttpMethod("DELETE"));
    }

    [Fact]
    public void GetHttpMethod_Options_Works()
    {
        Assert.Same(HttpMethod.Options, RequestUtilities.GetHttpMethod("OPTIONS"));
    }

    [Fact]
    public void GetHttpMethod_Head_Works()
    {
        Assert.Same(HttpMethod.Head, RequestUtilities.GetHttpMethod("HEAD"));
    }

    [Fact]
    public void GetHttpMethod_Patch_Works()
    {
        Assert.Same(HttpMethod.Patch, RequestUtilities.GetHttpMethod("PATCH"));
    }

    [Fact]
    public void GetHttpMethod_Trace_Works()
    {
        Assert.Same(HttpMethod.Trace, RequestUtilities.GetHttpMethod("TRACE"));
    }

    [Fact]
    public void GetHttpMethod_Unknown_Works()
    {
        Assert.Same("Unknown", RequestUtilities.GetHttpMethod("Unknown").Method);
    }

    [Fact]
    public void GetHttpMethod_Connect_Throws()
    {
        Assert.Throws<NotSupportedException>(() => RequestUtilities.GetHttpMethod("CONNECT"));
    }

    [Theory]
    [InlineData(" GET")]
    [InlineData("GET ")]
    [InlineData("G;ET")]
    public void GetHttpMethod_Invalid_Throws(string method)
    {
        Assert.Throws<FormatException>(() => RequestUtilities.GetHttpMethod(method));
    }

    [Theory]
    [InlineData("http://localhost", "", "", "http://localhost/")]
    [InlineData("http://localhost/", "", "", "http://localhost/")]
    [InlineData("http://localhost", "/", "", "http://localhost/")]
    [InlineData("http://localhost/", "/", "", "http://localhost/")]
    [InlineData("http://localhost", "", "?query", "http://localhost/?query")]
    [InlineData("http://localhost", "/path", "?query", "http://localhost/path?query")]
    [InlineData("http://localhost", "/path/", "?query", "http://localhost/path/?query")]
    [InlineData("http://localhost/", "/path", "?query", "http://localhost/path?query")]
    [InlineData("http://localhost/base", "", "", "http://localhost/base")]
    [InlineData("http://localhost/base", "", "?query", "http://localhost/base?query")]
    [InlineData("http://localhost/base", "/path", "?query", "http://localhost/base/path?query")]
    [InlineData("http://localhost/base/", "/path", "?query", "http://localhost/base/path?query")]
    [InlineData("http://localhost/base/", "/path/", "?query", "http://localhost/base/path/?query")]
    [InlineData("http://localhost/base/", "/path/你好", "?query%E4%BD%A0%E5%A5%BD", "http://localhost/base/path/%E4%BD%A0%E5%A5%BD?query%E4%BD%A0%E5%A5%BD")]
    // pchar         = unreserved / pct-encoded / sub-delims / ":" / "@"
    // unreserved    = ALPHA / DIGIT / "-" / "." / "_" / "~"
    // sub-delims    = "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="
    [InlineData("http://localhost/base/", "/path/!$&'()*+,;=:@/%?#[]", "?query", "http://localhost/base/path/!$&'()*+,;=:@/%25%3F%23%5B%5D?query")]
    // PathString should be fully un-escaped to start with and QueryString should be fully escaped.
    [InlineData("http://localhost/base/", "/path/%2F%20", "?query%20", "http://localhost/base/path/%252F%2520?query%20")]
    public void MakeDestinationAddress(string destinationPrefix, string path, string query, string expected)
    {
        var uri = RequestUtilities.MakeDestinationAddress(destinationPrefix, new PathString(path), new QueryString(query));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    // https://datatracker.ietf.org/doc/html/rfc3986/#appendix-A
    // pchar         = unreserved / pct-encoded / sub-delims / ":" / "@"
    // pct-encoded   = "%" HEXDIG HEXDIG
    // unreserved    = ALPHA / DIGIT / "-" / "." / "_" / "~"
    // reserved      = gen-delims / sub-delims
    // gen-delims    = ":" / "/" / "?" / "#" / "[" / "]" / "@"
    // sub-delims    = "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="

    [Fact]
    public void ValidPathCharacters()
    {
        var valids = new char[]
        {
            '!', '$', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            ':', ';', '=', '@',
            'A', 'B', 'C', 'D', 'E', 'F','G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '_',
            'a', 'b', 'c', 'd', 'e', 'f','g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '~'
        };

        foreach (var c in valids)
        {
            Assert.Equal($"/{c}", RequestUtilities.EncodePath($"/{c}"));
        }
    }

    [Fact]
    public void InvalidPathCharacters()
    {
        var invalids = new char[]
        {
            // Controls
            (char)0x00, (char)0x01, (char)0x02, (char)0x03, (char)0x04, (char)0x05, (char)0x06, (char)0x00, (char)0x07, (char)0x08, (char)0x09, (char)0x0A, (char)0x0B, (char)0x0C, (char)0x0D, (char)0x0E, (char)0x0F,
            (char)0x10, (char)0x11, (char)0x02, (char)0x13, (char)0x14, (char)0x15, (char)0x16, (char)0x10, (char)0x17, (char)0x18, (char)0x19, (char)0x1A, (char)0x1B, (char)0x1C, (char)0x1D, (char)0x1E, (char)0x1F,
            ' ', '"', '#', '%', '<', '>', '?', '[', '\\', ']', '^', '`', '{', '|', '}'
        };

        foreach (var c in invalids)
        {
            Assert.Equal($"/%{(int)c:X2}", RequestUtilities.EncodePath($"/{c}"));
        }
    }

    [Theory]
    [InlineData(null, "a", "a")]
    [InlineData("a", "", "a;")]
    [InlineData("", "a", ";a")]
    [InlineData("a", "b", "a;b")]
    [InlineData(null, "a;b", "a;b")]
    [InlineData("a;b", "", "a;b;")]
    [InlineData("", "a;b", ";a;b")]
    [InlineData("a;b", "c", "a;b;c")]
    [InlineData("a", "b;c", "a;b;c")]
    [InlineData("a;b", "c;d", "a;b;c;d")]
    [InlineData("a", "b c", "a;b c")]
    [InlineData("a b", "c", "a b;c")]
    public void Concat(string stringValues, string inputHeaderStringValues, string expectedOutput)
    {
        var request = new HttpRequestMessage();
        foreach (var value in inputHeaderStringValues.Split(';'))
        {
            request.Headers.TryAddWithoutValidation("foo", value);
        }
        request.Headers.TryAddWithoutValidation("bar", inputHeaderStringValues.Split(';'));

        var headerStringValues = request.Headers.NonValidated["foo"];
        var actualValues = RequestUtilities.Concat(stringValues?.Split(';'), headerStringValues);
        Assert.Equal(expectedOutput.Split(';'), actualValues);

        headerStringValues = request.Headers.NonValidated["bar"];
        actualValues = RequestUtilities.Concat(stringValues?.Split(';'), headerStringValues);
        Assert.Equal(expectedOutput.Split(';'), actualValues);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a b")]
    [InlineData("a", "b")]
    [InlineData("a", "b c", "d")]
    [InlineData("")]
    [InlineData("", "")]
    [InlineData("a", "")]
    [InlineData("", "a")]
    [InlineData("", "a", "b")]
    [InlineData("", "a", "")]
    [InlineData("a", "", "b")]
    public void TryGetValues(params string[] headerValues)
    {
        var request = new HttpRequestMessage();
        foreach (var value in headerValues)
        {
            request.Headers.TryAddWithoutValidation("foo", value);
        }
        request.Headers.TryAddWithoutValidation("bar", headerValues);

        Assert.True(RequestUtilities.TryGetValues(request.Headers, "foo", out var actualValues));
        Assert.Equal(headerValues, actualValues);

        Assert.True(RequestUtilities.TryGetValues(request.Headers, "bar", out actualValues));
        Assert.Equal(headerValues, actualValues);
    }
}
