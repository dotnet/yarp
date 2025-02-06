// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// APIs that can be used when transforming requests.
/// </summary>
public static class RequestUtilities
{
#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> s_validPathChars =
        SearchValues.Create("!$&'()*+,-./0123456789:;=@ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~");
#endif

    /// <summary>
    /// Converts the given HTTP method (usually obtained from <see cref="HttpRequest.Method"/>)
    /// into the corresponding <see cref="HttpMethod"/> static instance.
    /// </summary>
    internal static HttpMethod GetHttpMethod(string method) => method switch
    {
        string mth when HttpMethods.IsGet(mth) => HttpMethod.Get,
        string mth when HttpMethods.IsPost(mth) => HttpMethod.Post,
        string mth when HttpMethods.IsPut(mth) => HttpMethod.Put,
        string mth when HttpMethods.IsDelete(mth) => HttpMethod.Delete,
        string mth when HttpMethods.IsOptions(mth) => HttpMethod.Options,
        string mth when HttpMethods.IsHead(mth) => HttpMethod.Head,
        string mth when HttpMethods.IsPatch(mth) => HttpMethod.Patch,
        string mth when HttpMethods.IsTrace(mth) => HttpMethod.Trace,
        // NOTE: Proxying "CONNECT" is not supported (by design!)
        string mth when HttpMethods.IsConnect(mth) => throw new NotSupportedException($"Unsupported request method '{method}'."),
        _ => new HttpMethod(method)
    };

    internal static bool ShouldSkipRequestHeader(string headerName)
    {
        if (_headersToExclude.Contains(headerName))
        {
            return true;
        }

        // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
        if (headerName.StartsWith(':'))
        {
            return true;
        }

        return false;
    }

    internal static bool ShouldSkipResponseHeader(string headerName)
    {
        return _headersToExclude.Contains(headerName);
    }

    private static readonly FrozenSet<string> _headersToExclude = new HashSet<string>(17, StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Connection,
        HeaderNames.TransferEncoding,
        HeaderNames.KeepAlive,
        HeaderNames.Upgrade,
        HeaderNames.ProxyConnection,
        HeaderNames.ProxyAuthenticate,
        "Proxy-Authentication-Info",
        HeaderNames.ProxyAuthorization,
        "Proxy-Features",
        "Proxy-Instruction",
        "Security-Scheme",
        "ALPN",
        "Close",
        "HTTP2-Settings",
        HeaderNames.UpgradeInsecureRequests,
        HeaderNames.TE,
        HeaderNames.AltSvc,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Headers marked as HttpHeaderType.Content in
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/Headers/KnownHeaders.cs
    private static readonly FrozenSet<string> _contentHeaders = new HashSet<string>(11, StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Allow,
        HeaderNames.ContentDisposition,
        HeaderNames.ContentEncoding,
        HeaderNames.ContentLanguage,
        HeaderNames.ContentLength,
        HeaderNames.ContentLocation,
        HeaderNames.ContentMD5,
        HeaderNames.ContentRange,
        HeaderNames.ContentType,
        HeaderNames.Expires,
        HeaderNames.LastModified
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Appends the given path and query to the destination prefix while avoiding duplicate '/'.
    /// </summary>
    /// <param name="destinationPrefix">The scheme, host, port, and optional path base for the destination server.
    /// e.g. "http://example.com:80/path/prefix"</param>
    /// <param name="path">The path to append.</param>
    /// <param name="query">The query to append</param>
    public static Uri MakeDestinationAddress(string destinationPrefix, PathString path, QueryString query)
    {
        ReadOnlySpan<char> prefixSpan = destinationPrefix;

        if (path.HasValue && destinationPrefix.EndsWith('/'))
        {
            // When PathString has a value it always starts with a '/'. Avoid double slashes when concatenating.
            prefixSpan = prefixSpan[0..^1];
        }

        var targetAddress = string.Concat(prefixSpan, EncodePath(path), query.ToUriComponent());

        return new Uri(targetAddress, UriKind.Absolute);
    }

    // This isn't using PathString.ToUriComponent() because it doesn't round trip some escape sequences the way we want.
    private static string EncodePath(PathString path)
    {
        var value = path.Value;

        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Check if any escaping is required.
#if NET8_0_OR_GREATER
        var indexOfInvalidChar = value.AsSpan().IndexOfAnyExcept(s_validPathChars);
#else
        var indexOfInvalidChar = -1;

        for (var i = 0; i < value.Length; i++)
        {
            if (!IsValidPathChar(value[i]))
            {
                indexOfInvalidChar = i;
                break;
            }
        }
#endif

        return indexOfInvalidChar < 0
            ? value
            : EncodePath(value, indexOfInvalidChar);
    }

    private static string EncodePath(string value, int i)
    {
        var builder = new ValueStringBuilder(stackalloc char[ValueStringBuilder.StackallocThreshold]);

        var start = 0;
        var count = i;
        var requiresEscaping = false;

        while (i < value.Length)
        {
            if (IsValidPathChar(value[i]))
            {
                if (requiresEscaping)
                {
                    // the current segment requires escape
                    builder.Append(Uri.EscapeDataString(value.Substring(start, count)));

                    requiresEscaping = false;
                    start = i;
                    count = 0;
                }

                count++;
                i++;
            }
            else
            {
                if (!requiresEscaping)
                {
                    // the current segment doesn't require escape
                    builder.Append(value.AsSpan(start, count));

                    requiresEscaping = true;
                    start = i;
                    count = 0;
                }

                count++;
                i++;
            }
        }

        Debug.Assert(count > 0);

        if (requiresEscaping)
        {
            builder.Append(Uri.EscapeDataString(value.Substring(start, count)));
        }
        else
        {
            builder.Append(value.AsSpan(start, count));
        }

        return builder.ToString();
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValidPathChar(char c) => s_validPathChars.Contains(c);
#else
    // https://datatracker.ietf.org/doc/html/rfc3986/#appendix-A
    // pchar         = unreserved / pct-encoded / sub-delims / ":" / "@"
    // pct-encoded   = "%" HEXDIG HEXDIG
    // unreserved    = ALPHA / DIGIT / "-" / "." / "_" / "~"
    // reserved      = gen-delims / sub-delims
    // gen-delims    = ":" / "/" / "?" / "#" / "[" / "]" / "@"
    // sub-delims    = "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="

    // uint[] bits uses 1 cache line (Array info + 16 bytes)
    // bool[] would use 3 cache lines (Array info + 128 bytes)
    // So we use 128 bits rather than 128 bytes/bools
    private static readonly uint[] ValidPathChars = {
        0b_0000_0000__0000_0000__0000_0000__0000_0000, // 0x00 - 0x1F
        0b_0010_1111__1111_1111__1111_1111__1101_0010, // 0x20 - 0x3F
        0b_1000_0111__1111_1111__1111_1111__1111_1111, // 0x40 - 0x5F
        0b_0100_0111__1111_1111__1111_1111__1111_1110, // 0x60 - 0x7F
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValidPathChar(char c)
    {
        // Use local array and uint .Length compare to elide the bounds check on array access
        var validChars = ValidPathChars;
        var i = (int)c;

        // Array is in chunks of 32 bits, so get offset by dividing by 32
        var offset = i >> 5; // i / 32;
        // Significant bit position is the remainder of the above calc; i % 32 => i & 31
        var significantBit = 1u << (i & 31);

        // Check offset in bounds and check if significant bit set
        return (uint)offset < (uint)validChars.Length &&
            ((validChars[offset] & significantBit) != 0);
    }
#endif

    // Note: HttpClient.SendAsync will end up sending the union of
    // HttpRequestMessage.Headers and HttpRequestMessage.Content.Headers.
    // We don't really care where the proxied headers appear among those 2,
    // as long as they appear in one (and only one, otherwise they would be duplicated).
    // Some headers may only appear on HttpContentHeaders, in which case we inject
    // an EmptyHttpContent - dummy 0-length container only used for headers.
    internal static void AddHeader(HttpRequestMessage request, string headerName, StringValues value)
    {
        if (value.Count == 1)
        {
            string headerValue = value!;

            if (ContainsNewLines(headerValue))
            {
                // TODO: Log
                return;
            }

            if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
            {
                if (request.Content is null && _contentHeaders.Contains(headerName))
                {
                    request.Content = new EmptyHttpContent();
                }

                var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValue);
                // TODO: Log
                Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {headerValue}");
            }
        }
        else
        {
            string[] headerValues = value!;

#if !NET7_0_OR_GREATER
            // HttpClient wrongly uses comma (",") instead of semi-colon (";") as a separator for Cookie headers.
            // To mitigate this, we concatenate them manually and put them back as a single header value.
            // A multi-header cookie header is invalid, but we get one because of
            // https://github.com/dotnet/aspnetcore/issues/26461
            if (string.Equals(headerName, HeaderNames.Cookie, StringComparison.OrdinalIgnoreCase))
            {
                AddHeader(request, headerName, string.Join("; ", headerValues));
                return;
            }
#endif

            foreach (var headerValue in headerValues)
            {
                if (ContainsNewLines(headerValue))
                {
                    // TODO: Log
                    return;
                }
            }

            if (!request.Headers.TryAddWithoutValidation(headerName, headerValues))
            {
                if (request.Content is null && _contentHeaders.Contains(headerName))
                {
                    request.Content = new EmptyHttpContent();
                }

                var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValues);
                // TODO: Log
                Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {string.Join(", ", headerValues)}");
            }
        }

#if DEBUG
        if (request.Content is EmptyHttpContent content && content.Headers.TryGetValues(HeaderNames.ContentLength, out var contentLength))
        {
            Debug.Assert(contentLength.Single() == "0", "An actual content should have been set");
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ContainsNewLines(string value) => value.AsSpan().IndexOfAny('\r', '\n') >= 0;
    }

    internal static void RemoveHeader(HttpRequestMessage request, string headerName)
    {
        if (_contentHeaders.Contains(headerName))
        {
            request.Content?.Headers.Remove(headerName);
        }
        else
        {
            request.Headers.Remove(headerName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StringValues Concat(in StringValues existing, in HeaderStringValues values)
    {
        if (values.Count <= 1)
        {
            return StringValues.Concat(existing, values.ToString());
        }
        else
        {
            return ConcatSlow(existing, values);
        }

        static StringValues ConcatSlow(in StringValues existing, in HeaderStringValues values)
        {
            Debug.Assert(values.Count > 1);

            var count = existing.Count;
            var newArray = new string[count + values.Count];

            if (count == 1)
            {
                newArray[0] = existing.ToString();
            }
            else
            {
                existing.ToArray().CopyTo(newArray, 0);
            }

            foreach (var value in values)
            {
                newArray[count++] = value;
            }
            Debug.Assert(count == newArray.Length);

            return newArray;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetValues(HttpHeaders headers, string headerName, out StringValues values)
    {
        if (headers.NonValidated.TryGetValues(headerName, out var headerStringValues))
        {
            if (headerStringValues.Count <= 1)
            {
                values = headerStringValues.ToString();
            }
            else
            {
                values = ToArray(headerStringValues);
            }
            return true;
        }

        static StringValues ToArray(in HeaderStringValues values)
        {
            var array = new string[values.Count];
            var i = 0;
            foreach (var value in values)
            {
                array[i++] = value;
            }
            Debug.Assert(i == array.Length);
            return array;
        }

        values = default;
        return false;
    }

    internal static bool IsResponseSet(HttpResponse response)
    {
        return response.StatusCode != StatusCodes.Status200OK
            || response.HasStarted;
    }
}
