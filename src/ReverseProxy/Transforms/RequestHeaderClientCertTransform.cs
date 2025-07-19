// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Base64 encodes the client certificate (if any) and sets it as the header value.
/// </summary>
public class RequestHeaderClientCertTransform : RequestTransform
{
    public RequestHeaderClientCertTransform(string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
    }

    internal string HeaderName { get; }

    /// <inheritdoc/>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RemoveHeader(context, HeaderName);

        var clientCert = context.HttpContext.Connection.ClientCertificate;
        if (clientCert is not null)
        {
            var encoded = Convert.ToBase64String(clientCert.RawData);
            AddHeader(context, HeaderName, encoded);
        }

        return default;
    }
}
