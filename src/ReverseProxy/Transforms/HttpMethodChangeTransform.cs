// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Replaces the HTTP method if it matches.
/// </summary>
public class HttpMethodChangeTransform : RequestTransform
{
    /// <summary>
    /// Creates a new transform.
    /// </summary>
    /// <param name="fromMethod">The method to match.</param>
    /// <param name="toMethod">The method to it change to.</param>
    public HttpMethodChangeTransform(string fromMethod, string toMethod)
    {
        if (string.IsNullOrEmpty(fromMethod))
        {
            throw new ArgumentException($"'{nameof(fromMethod)}' cannot be null or empty.", nameof(fromMethod));
        }

        if (string.IsNullOrEmpty(toMethod))
        {
            throw new ArgumentException($"'{nameof(toMethod)}' cannot be null or empty.", nameof(toMethod));
        }

        FromMethod = GetCanonicalizedValue(fromMethod);
        ToMethod = GetCanonicalizedValue(toMethod);
    }

    internal HttpMethod FromMethod { get; }

    internal HttpMethod ToMethod { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (FromMethod.Equals(context.ProxyRequest.Method))
        {
            context.ProxyRequest.Method = ToMethod;
        }

        return default;
    }

    private static HttpMethod GetCanonicalizedValue(string method)
    {
        return method switch
        {
            string _ when HttpMethods.IsGet(method) => HttpMethod.Get,
            string _ when HttpMethods.IsPost(method) => HttpMethod.Post,
            string _ when HttpMethods.IsPut(method) => HttpMethod.Put,
            string _ when HttpMethods.IsDelete(method) => HttpMethod.Delete,
            string _ when HttpMethods.IsOptions(method) => HttpMethod.Options,
            string _ when HttpMethods.IsHead(method) => HttpMethod.Head,
            string _ when HttpMethods.IsPatch(method) => HttpMethod.Patch,
            string _ when HttpMethods.IsTrace(method) => HttpMethod.Trace,
            string _ => new HttpMethod(method),
        };
    }
}
