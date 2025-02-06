// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Errors reported when forwarding a request to the destination.
/// </summary>
public enum ForwarderError : int
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// Failed to connect, send the request headers, or receive the response headers.
    /// </summary>
    Request,

    /// <summary>
    /// Timed out when trying to connect, send the request headers, or receive the response headers.
    /// </summary>
    RequestTimedOut,

    /// <summary>
    /// Canceled when trying to connect, send the request headers, or receive the response headers.
    /// </summary>
    RequestCanceled,

    /// <summary>
    /// Canceled while copying the request body.
    /// </summary>
    RequestBodyCanceled,

    /// <summary>
    /// Failed reading the request body from the client.
    /// </summary>
    RequestBodyClient,

    /// <summary>
    /// Failed writing the request body to the destination.
    /// </summary>
    RequestBodyDestination,

    /// <summary>
    /// Failed to copy response headers.
    /// </summary>
    ResponseHeaders,

    /// <summary>
    /// Canceled while copying the response body.
    /// </summary>
    ResponseBodyCanceled,

    /// <summary>
    /// Failed when writing response body to the client.
    /// </summary>
    ResponseBodyClient,

    /// <summary>
    /// Failed when reading response body from the destination.
    /// </summary>
    ResponseBodyDestination,

    /// <summary>
    /// Canceled while copying the upgraded response body.
    /// </summary>
    UpgradeRequestCanceled,

    /// <summary>
    /// Failed reading the upgraded request body from the client.
    /// </summary>
    UpgradeRequestClient,

    /// <summary>
    /// Failed writing the upgraded request body to the destination.
    /// </summary>
    UpgradeRequestDestination,

    /// <summary>
    /// Canceled while copying the upgraded response body.
    /// </summary>
    UpgradeResponseCanceled,

    /// <summary>
    /// Failed when writing the upgraded response body to the client.
    /// </summary>
    UpgradeResponseClient,

    /// <summary>
    /// Failed when reading the upgraded response body from the destination.
    /// </summary>
    UpgradeResponseDestination,

    /// <summary>
    /// Indicates there were no destinations remaining to proxy the request to.
    /// The configured destinations may have been excluded due to heath or other considerations.
    /// </summary>
    NoAvailableDestinations,

    /// <summary>
    /// Failed while creating the request message.
    /// </summary>
    RequestCreation,

    /// <summary>
    /// An upgraded request was idle and canceled due to the activity timeout.
    /// </summary>
    UpgradeActivityTimeout,
}
