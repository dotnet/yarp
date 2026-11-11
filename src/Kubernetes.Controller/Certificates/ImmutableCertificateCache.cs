// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable
namespace Yarp.Kubernetes.Controller.Certificates;

public abstract class ImmutableCertificateCache<TCert> where TCert : class
{
    private readonly List<WildCardDomain> _wildCardDomains = new();
    private readonly Dictionary<string, TCert> _certificates = new(StringComparer.OrdinalIgnoreCase);

    public ImmutableCertificateCache(IEnumerable<TCert> certificates, Func<TCert, IEnumerable<string>> getDomains)
    {
        foreach (var certificate in certificates)
        {
            foreach (var domain in getDomains(certificate))
            {
                if (domain.StartsWith("*."))
                {
                    _wildCardDomains.Add(new (domain[1..], certificate));
                }
                else
                {
                    _certificates[domain] = certificate;
                }
            }
        }

        _wildCardDomains.Sort(DomainNameComparer.Instance);
    }



    protected abstract TCert? GetDefaultCertificate();

    public TCert? GetCertificate(string domain)
    {
        if (TryGetCertificateExact(domain, out var certificate))
        {
            return certificate;
        }
        if (TryGetWildcardCertificate(domain, out certificate))
        {
            return certificate;
        }

        return GetDefaultCertificate();
    }

    protected IReadOnlyList<WildCardDomain> WildcardCertificates => _wildCardDomains;

    protected IReadOnlyDictionary<string, TCert> Certificates => _certificates;

    protected record struct WildCardDomain(string Domain, TCert? Certificate);

    private bool TryGetCertificateExact(string domain, [NotNullWhen(true)] out TCert? certificate) =>
        _certificates.TryGetValue(domain, out certificate);

    private bool TryGetWildcardCertificate(string domain, [NotNullWhen(true)] out TCert? certificate)
    {
        if (_wildCardDomains.BinarySearch(new WildCardDomain(domain, null!), DomainNameComparer.Instance) is { } index and > -1)
        {
            certificate = _wildCardDomains[index].Certificate!;
            return true;
        }

        certificate = null;
        return false;
    }

    /// <summary>
    /// Sorts domain names right to left.
    /// This allows us to use a Binary Search to achieve a suffix
    /// search.
    /// </summary>
    private class DomainNameComparer : IComparer<WildCardDomain>
    {
        public static readonly DomainNameComparer Instance = new();

        public int Compare(WildCardDomain x, WildCardDomain y)
        {
            var ret = Compare(x.Domain.AsSpan(), y.Domain.AsSpan());
            if (ret != 0)
            {
                return ret;
            }

            return (x.Certificate, y.Certificate) switch
            {
                (null, not null) when x.Domain.Length > y.Domain.Length => 0,
                (not null, null) when x.Domain.Length < y.Domain.Length => 0,
                _ => x.Domain.Length - y.Domain.Length
            };
        }

        private static int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {

            var length = Math.Min(x.Length, y.Length);
            x = x[^length..];
            y = y[^length..];

            for (var index = length - 1; index >= 0; index--)
            {
                var charA = x[index] & 0x5F;
                var charB = y[index] & 0x5F;

                if (charA == charB)
                {
                    continue;
                }

                return charB - charA;
            }

            return 0;
        }
    }
}
