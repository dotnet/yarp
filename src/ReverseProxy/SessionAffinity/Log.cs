// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.SessionAffinity;

internal static class Log
{
    private static readonly Action<ILogger, string, Exception?> _affinityCannotBeEstablishedBecauseNoDestinationsFound = LoggerMessage.Define<string>(
        LogLevel.Warning,
        EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster,
        "The request affinity cannot be established because no destinations are found on cluster `{clusterId}`.");

    private static readonly Action<ILogger, Exception?> _requestAffinityKeyDecryptionFailed = LoggerMessage.Define(
        LogLevel.Error,
        EventIds.RequestAffinityKeyDecryptionFailed,
        "The request affinity key decryption failed.");

    private static readonly Action<ILogger, string, Exception?> _destinationMatchingToAffinityKeyNotFound = LoggerMessage.Define<string>(
        LogLevel.Warning,
        EventIds.DestinationMatchingToAffinityKeyNotFound,
        "Destination matching to the request affinity key is not found on cluster `{clusterId}`. Configured failure policy will be applied.");

    public static void AffinityCannotBeEstablishedBecauseNoDestinationsFound(ILogger logger, string clusterId)
    {
        _affinityCannotBeEstablishedBecauseNoDestinationsFound(logger, clusterId, null);
    }

    public static void RequestAffinityKeyDecryptionFailed(ILogger logger, Exception? ex)
    {
        _requestAffinityKeyDecryptionFailed(logger, ex);
    }

    public static void DestinationMatchingToAffinityKeyNotFound(ILogger logger, string clusterId)
    {
        _destinationMatchingToAffinityKeyNotFound(logger, clusterId, null);
    }
}
