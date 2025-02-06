// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Health;

internal partial class ActiveHealthCheckMonitor
{
    private static class Log
    {
        private static readonly Action<ILogger, Exception> _explicitActiveCheckOfAllClustersHealthFailed = LoggerMessage.Define(
            LogLevel.Error,
            EventIds.ExplicitActiveCheckOfAllClustersHealthFailed,
            "An explicitly started active check of all clusters health failed.");

        private static readonly Action<ILogger, string, Exception> _activeHealthProbingFailedOnCluster = LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.ActiveHealthProbingFailedOnCluster,
            "Active health probing failed on cluster `{clusterId}`.");

        private static readonly Action<ILogger, string, Exception> _errorOccuredDuringActiveHealthProbingShutdownOnCluster = LoggerMessage.Define<string>(
            LogLevel.Error,
            EventIds.ErrorOccuredDuringActiveHealthProbingShutdownOnCluster,
            "An error occured during shutdown of an active health probing on cluster `{clusterId}`.");

        private static readonly Action<ILogger, string, string, Exception> _activeHealthProbeConstructionFailedOnCluster = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            EventIds.ActiveHealthProbeConstructionFailedOnCluster,
            "Construction of an active health probe for destination `{destinationId}` on cluster `{clusterId}` failed.");

        private static readonly Action<ILogger, string, Exception?> _startingActiveHealthProbingOnCluster = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.StartingActiveHealthProbingOnCluster,
            "Starting active health check probing on cluster `{clusterId}`.");

        private static readonly Action<ILogger, string, Exception?> _stoppedActiveHealthProbingOnCluster = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.StoppedActiveHealthProbingOnCluster,
            "Active health check probing on cluster `{clusterId}` has stopped.");

        private static readonly Action<ILogger, string, string, int, Exception?> _destinationProbingCompleted = LoggerMessage.Define<string, string, int>(
            LogLevel.Information,
            EventIds.DestinationProbingCompleted,
            "Probing destination `{destinationId}` on cluster `{clusterId}` completed with the response code `{responseCode}`.");

        private static readonly Action<ILogger, string, string, Exception> _destinationProbingFailed = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            EventIds.DestinationProbingFailed,
            "Probing destination `{destinationId}` on cluster `{clusterId}` failed.");

        private static readonly Action<ILogger, Uri?, string, string, Exception?> _sendingHealthProbeToEndpointOfDestination = LoggerMessage.Define<Uri?, string, string>(
            LogLevel.Debug,
            EventIds.SendingHealthProbeToEndpointOfDestination,
            "Sending a health probe to endpoint `{endpointUri}` of destination `{destinationId}` on cluster `{clusterId}`.");

        public static void ExplicitActiveCheckOfAllClustersHealthFailed(ILogger logger, Exception ex)
        {
            _explicitActiveCheckOfAllClustersHealthFailed(logger, ex);
        }

        public static void ActiveHealthProbingFailedOnCluster(ILogger logger, string clusterId, Exception ex)
        {
            _activeHealthProbingFailedOnCluster(logger, clusterId, ex);
        }

        public static void ErrorOccuredDuringActiveHealthProbingShutdownOnCluster(ILogger logger, string clusterId, Exception ex)
        {
            _errorOccuredDuringActiveHealthProbingShutdownOnCluster(logger, clusterId, ex);
        }

        public static void ActiveHealthProbeConstructionFailedOnCluster(ILogger logger, string destinationId, string clusterId, Exception ex)
        {
            _activeHealthProbeConstructionFailedOnCluster(logger, destinationId, clusterId, ex);
        }

        public static void StartingActiveHealthProbingOnCluster(ILogger logger, string clusterId)
        {
            _startingActiveHealthProbingOnCluster(logger, clusterId, null);
        }

        public static void StoppedActiveHealthProbingOnCluster(ILogger logger, string clusterId)
        {
            _stoppedActiveHealthProbingOnCluster(logger, clusterId, null);
        }

        public static void DestinationProbingCompleted(ILogger logger, string destinationId, string clusterId, int responseCode)
        {
            _destinationProbingCompleted(logger, destinationId, clusterId, responseCode, null);
        }

        public static void DestinationProbingFailed(ILogger logger, string destinationId, string clusterId, Exception ex)
        {
            _destinationProbingFailed(logger, destinationId, clusterId, ex);
        }

        public static void SendingHealthProbeToEndpointOfDestination(ILogger logger, Uri? endpointUri, string destinationId, string clusterId)
        {
            _sendingHealthProbeToEndpointOfDestination(logger, endpointUri, destinationId, clusterId, null);
        }
    }
}
