// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy;

internal static class EventIds
{
    public static readonly EventId LoadData = new EventId(1, "ApplyProxyConfig");
    public static readonly EventId ErrorSignalingChange = new EventId(2, "ApplyProxyConfigFailed");
    public static readonly EventId NoClusterFound = new EventId(4, "NoClusterFound");
    public static readonly EventId NoAvailableDestinations = new EventId(7, "NoAvailableDestinations");
    public static readonly EventId MultipleDestinationsAvailable = new EventId(8, "MultipleDestinationsAvailable");
    public static readonly EventId Forwarding = new EventId(9, "Forwarding");
    public static readonly EventId ExplicitActiveCheckOfAllClustersHealthFailed = new EventId(10, "ExplicitActiveCheckOfAllClustersHealthFailed");
    public static readonly EventId ActiveHealthProbingFailedOnCluster = new EventId(11, "ActiveHealthProbingFailedOnCluster");
    public static readonly EventId ErrorOccurredDuringActiveHealthProbingShutdownOnCluster = new EventId(12, "ErrorOccurredDuringActiveHealthProbingShutdownOnCluster");
    public static readonly EventId ActiveHealthProbeConstructionFailedOnCluster = new EventId(13, "ActiveHealthProbeConstructionFailedOnCluster");
    public static readonly EventId StartingActiveHealthProbingOnCluster = new EventId(14, "StartingActiveHealthProbingOnCluster");
    public static readonly EventId StoppedActiveHealthProbingOnCluster = new EventId(15, "StoppedActiveHealthProbingOnCluster");
    public static readonly EventId DestinationProbingCompleted = new EventId(16, "DestinationActiveProbingCompleted");
    public static readonly EventId DestinationProbingFailed = new EventId(17, "DestinationActiveProbingFailed");
    public static readonly EventId SendingHealthProbeToEndpointOfDestination = new EventId(18, "SendingHealthProbeToEndpointOfDestination");
    public static readonly EventId UnhealthyDestinationIsScheduledForReactivation = new EventId(19, "UnhealthyDestinationIsScheduledForReactivation");
    public static readonly EventId PassiveDestinationHealthResetToUnknownState = new EventId(20, "PassiveDestinationHealthResetToUnknownState");
    public static readonly EventId ClusterAdded = new EventId(21, "ClusterAdded");
    public static readonly EventId ClusterChanged = new EventId(22, "ClusterChanged");
    public static readonly EventId ClusterRemoved = new EventId(23, "ClusterRemoved");
    public static readonly EventId DestinationAdded = new EventId(24, "EndpointAdded");
    public static readonly EventId DestinationChanged = new EventId(25, "EndpointChanged");
    public static readonly EventId DestinationRemoved = new EventId(26, "EndpointRemoved");
    public static readonly EventId RouteAdded = new EventId(27, "RouteAdded");
    public static readonly EventId RouteChanged = new EventId(28, "RouteChanged");
    public static readonly EventId RouteRemoved = new EventId(29, "RouteRemoved");
    public static readonly EventId HttpDowngradeDetected = new EventId(30, "HttpDowngradeDetected");
    public static readonly EventId OperationStarted = new EventId(31, "OperationStarted");
    public static readonly EventId OperationEnded = new EventId(32, "OperationEnded");
    public static readonly EventId OperationFailed = new EventId(33, "OperationFailed");
    public static readonly EventId AffinityResolutionFailedForCluster = new EventId(34, "AffinityResolutionFailedForCluster");
    public static readonly EventId MultipleDestinationsOnClusterToEstablishRequestAffinity = new EventId(35, "MultipleDestinationsOnClusterToEstablishRequestAffinity");
    public static readonly EventId AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster = new EventId(36, "AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster");
    public static readonly EventId NoDestinationOnClusterToEstablishRequestAffinity = new EventId(37, "NoDestinationOnClusterToEstablishRequestAffinity");
    public static readonly EventId RequestAffinityKeyDecryptionFailed = new EventId(38, "RequestAffinityKeyDecryptionFailed");
    public static readonly EventId DestinationMatchingToAffinityKeyNotFound = new EventId(39, "DestinationMatchingToAffinityKeyNotFound");
    public static readonly EventId RequestAffinityHeaderHasMultipleValues = new EventId(40, "RequestAffinityHeaderHasMultipleValues");
    public static readonly EventId AffinityResolutionFailureWasHandledProcessingWillBeContinued = new EventId(41, "AffinityResolutionFailureWasHandledProcessingWillBeContinued");
    public static readonly EventId ClusterConfigException = new EventId(42, "ClusterConfigException");
    public static readonly EventId ErrorReloadingConfig = new EventId(43, "ErrorReloadingConfig");
    public static readonly EventId ErrorApplyingConfig = new EventId(44, "ErrorApplyingConfig");
    public static readonly EventId ClientCreated = new EventId(45, "ClientCreated");
    public static readonly EventId ClientReused = new EventId(46, "ClientReused");
    public static readonly EventId ConfigurationDataConversionFailed = new EventId(47, "ConfigurationDataConversionFailed");
    public static readonly EventId ForwardingError = new EventId(48, "ForwardingError");
    public static readonly EventId ActiveDestinationHealthStateIsSetToUnhealthy = new EventId(49, "ActiveDestinationHealthStateIsSetToUnhealthy");
    public static readonly EventId ActiveDestinationHealthStateIsSet = new EventId(50, "ActiveDestinationHealthStateIsSet");
    public static readonly EventId DelegationQueueInitializationFailed = new EventId(51, "DelegationQueueInitializationFailed");
    public static readonly EventId DelegationQueueNotFound = new EventId(52, "DelegationQueueNotFound");
    public static readonly EventId DelegationQueueNotInitialized = new EventId(53, "DelegationQueueNotInitialized");
    public static readonly EventId DelegatingRequest = new EventId(54, "DelegatingRequest");
    public static readonly EventId DelegationFailed = new EventId(55, "DelegationFailed");
    public static readonly EventId ResponseReceived = new EventId(56, "ResponseReceived");
    public static readonly EventId DelegationQueueReset = new EventId(57, "DelegationQueueReset");
    public static readonly EventId Http10RequestVersionDetected = new EventId(58, "Http10RequestVersionDetected");
    public static readonly EventId NotForwarding = new EventId(59, "NotForwarding");
    public static readonly EventId MaxRequestBodySizeSet = new EventId(60, "MaxRequestBodySizeSet");
    public static readonly EventId RetryingWebSocketDowngradeNoConnect = new EventId(61, "RetryingWebSocketDowngradeNoConnect");
    public static readonly EventId RetryingWebSocketDowngradeNoHttp2 = new EventId(62, "RetryingWebSocketDowngradeNoHttp2");
    public static readonly EventId InvalidSecWebSocketKeyHeader = new EventId(63, "InvalidSecWebSocketKeyHeader");
    public static readonly EventId TimeoutNotApplied = new(64, nameof(TimeoutNotApplied));
    public static readonly EventId DelegationQueueNoLongerExists = new(65, nameof(DelegationQueueNoLongerExists));
    public static readonly EventId ForwardingRequestCancelled = new(66, nameof(ForwardingRequestCancelled));
    public static readonly EventId DelegationQueueDisposed = new(67, nameof(DelegationQueueDisposed));
}
