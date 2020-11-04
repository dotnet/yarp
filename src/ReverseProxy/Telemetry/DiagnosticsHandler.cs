// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// DiagnosticHandler notifies DiagnosticSource subscribers about outgoing Http requests
    /// </summary>
    internal sealed class DiagnosticsHandler : DelegatingHandler
    {
        /// <summary>
        /// DiagnosticHandler constructor
        /// </summary>
        /// <param name="innerHandler">Inner handler: Windows or Unix implementation of HttpMessageHandler.
        /// Note that DiagnosticHandler is the latest in the pipeline </param>
        public DiagnosticsHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // HttpClientHandler is responsible to call static DiagnosticsHandler.IsEnabled() before forwarding request here.
            // It will check if propagation is on (because parent Activity exists or there is a listener) or off (forcibly disabled)
            // This code won't be called unless consumer unsubscribes from DiagnosticListener right after the check.
            // So some requests happening right after subscription starts might not be instrumented. Similarly,
            // when consumer unsubscribes, extra requests might be instrumented

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request)); //, SR.net_http_handler_norequest); TODO: Is this really necessary?
            }

            // If we are on at all, we propagate current activity information
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                InjectHeaders(currentActivity, request);
            }

            try
            {
                var responseTask = base.SendAsync(request, cancellationToken);

                return await responseTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // we'll report task status in HttpRequestOut.Stop
                throw;
            }
        }

        #region private

        private void InjectHeaders(Activity currentActivity, HttpRequestMessage request)
        {
            if (currentActivity.IdFormat == ActivityIdFormat.W3C)
            {
                request.Headers.Remove(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName);

                if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName))
                {
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName, currentActivity.Id);
                    if (currentActivity.TraceStateString != null)
                    {
                        request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.TraceStateHeaderName, currentActivity.TraceStateString);
                    }
                }
            }
            else
            {
                request.Headers.Remove(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName);

                if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName))
                {
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName, currentActivity.Id);
                }
            }

            // we expect baggage to be empty or contain a few items
            using (var e = currentActivity.Baggage.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    var baggage = new List<string>();
                    do
                    {
                        var item = e.Current;
                        baggage.Add(new NameValueHeaderValue(Uri.EscapeDataString(item.Key), Uri.EscapeDataString(item.Value)).ToString());
                    }
                    while (e.MoveNext());
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.CorrelationContextHeaderName, baggage);
                }
            }
        }

        #endregion
    }
}
