using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Outgoing request configuration.
    ///
    /// Readonly copy of <see cref="Microsoft.ReverseProxy.Abstractions.ProxyHttpRequestOptions"/>.
    /// </summary>
    public readonly struct ClusterProxyHttpRequestOptions
    {
#if NET
        public ClusterProxyHttpRequestOptions(TimeSpan? timeout, Version version, HttpVersionPolicy? versionPolicy)
        {
            Timeout = timeout;
            Version = version;
            VersionPolicy = versionPolicy;
        }
#endif

        public ClusterProxyHttpRequestOptions(TimeSpan? timeout, Version version)
        {
            Timeout = timeout;
            Version = version;
#if NET
            VersionPolicy = null;
#endif
        }

        /// <summary>
        /// The time allowed to send the request and receive the response headers. This may include
        /// the time needed to send the request body. The default is 100 seconds.
        /// </summary>
        public TimeSpan? Timeout { get; }

        /// <summary>
        /// Preferred version of the outgoing request.
        /// The default is HTTP/2.0.
        /// </summary>
        public Version Version { get; }

#if NET
        /// <summary>
        /// The policy applied to version selection, e.g. whether to prefer downgrades, upgrades or
        /// request an exact version. The default is `RequestVersionOrLower`.
        /// </summary>
        public HttpVersionPolicy? VersionPolicy { get; }
#endif
    }
}
