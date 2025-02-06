// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class CustomHeaderSessionAffinityPolicy : BaseEncryptedSessionAffinityPolicy<string>
{
    public CustomHeaderSessionAffinityPolicy(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CustomHeaderSessionAffinityPolicy> logger)
        : base(dataProtectionProvider, logger)
    { }

    public override string Name => SessionAffinityConstants.Policies.CustomHeader;

    protected override string GetDestinationAffinityKey(DestinationState destination)
    {
        return destination.DestinationId;
    }

    protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config)
    {
        var customHeaderName = config.AffinityKeyName;
        var keyHeaderValues = context.Request.Headers[customHeaderName];

        if (StringValues.IsNullOrEmpty(keyHeaderValues))
        {
            // It means affinity key is not defined that is a successful case
            return (Key: null, ExtractedSuccessfully: true);
        }

        if (keyHeaderValues.Count > 1)
        {
            // Multiple values is an ambiguous case which is considered a key extraction failure
            Log.RequestAffinityHeaderHasMultipleValues(Logger, customHeaderName, keyHeaderValues.Count);
            return (Key: null, ExtractedSuccessfully: false);
        }

        return Unprotect(keyHeaderValues[0]);
    }

    protected override void SetAffinityKey(HttpContext context, ClusterState cluster, SessionAffinityConfig config, string unencryptedKey)
    {
        context.Response.Headers.Append(config.AffinityKeyName, Protect(unencryptedKey));
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, int, Exception?> _requestAffinityHeaderHasMultipleValues = LoggerMessage.Define<string, int>(
            LogLevel.Error,
            EventIds.RequestAffinityHeaderHasMultipleValues,
            "The request affinity header `{headerName}` has `{valueCount}` values.");

        public static void RequestAffinityHeaderHasMultipleValues(ILogger logger, string headerName, int valueCount)
        {
            _requestAffinityHeaderHasMultipleValues(logger, headerName, valueCount, null);
        }
    }
}
