// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Yarp.ReverseProxy;

public static class TestUrlHelper
{
    public static string GetTestUrl()
    {
        return BuildTestUri().ToString();
    }

    public static Uri BuildTestUri()
    {
        return BuildTestUri(Uri.UriSchemeHttp);
    }

    internal static Uri BuildTestUri(string scheme)
    {
        // Most functional tests use this codepath and should directly bind to dynamic port "0" and scrape
        // the assigned port from the status message, which should be 100% reliable since the port is bound
        // once and never released.  Binding to dynamic port "0" on "localhost" (both IPv4 and IPv6) is not
        // supported, so the port is only bound on "127.0.0.1" (IPv4).  If a test explicitly requires IPv6,
        // it should provide a hint URL with "localhost" (IPv4 and IPv6) or "[::1]" (IPv6-only).
        return new UriBuilder(scheme, "127.0.0.1", 0).Uri;
    }
}
