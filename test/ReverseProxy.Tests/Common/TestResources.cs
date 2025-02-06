// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Xunit;

namespace Yarp.Tests.Common;

public static class TestResources
{
    private const int MutexTimeout = 120 * 1000;
    private static readonly Mutex importPfxMutex = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
        new Mutex(initiallyOwned: false, "Global\\Yarp.ReverseProxy.Tests.Certificates.LoadPfxCertificate") :
        null;

    public static X509Certificate2 GetTestCertificate(string certName = "testCert.pfx")
    {
        // On Windows, applications should not import PFX files in parallel to avoid a known system-level
        // race condition bug in native code which can cause crashes/corruption of the certificate state.
        if (importPfxMutex is not null)
        {
            Assert.True(importPfxMutex.WaitOne(MutexTimeout), "Cannot acquire the global certificate mutex.");
        }

        try
        {
            return new X509Certificate2(GetCertPath(certName), "testPassword");
        }
        finally
        {
            importPfxMutex?.ReleaseMutex();
        }
    }

    public static IWebProxy GetTestWebProxy(string address = "http://localhost:8080", bool? bypassOnLocal = null, bool? useDefaultCredentials = null)
    {
        var webProxy = new WebProxy(new System.Uri(address));

        if (bypassOnLocal is not null)
        {
            webProxy.BypassProxyOnLocal = bypassOnLocal.Value;
        }

        if (useDefaultCredentials is not null)
        {
            webProxy.UseDefaultCredentials = useDefaultCredentials.Value;
        }

        return webProxy;
    }

    public static string GetCertPath(string fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "TestCertificates");
        return Path.Combine(basePath, fileName);
    }

    public static IEnumerable<(string Name, string[] Values)> ParseNameAndValues(string names, string values) =>
        names.Split("; ").Zip(values.Split(", ")).GroupBy(p => p.First, (k, g) => (Name: k, Values: g.Select(i => i.Second).ToArray()));
}
