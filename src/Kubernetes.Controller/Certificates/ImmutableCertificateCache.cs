// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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

    protected record WildCardDomain(string Domain, TCert? Certificate);

    private bool TryGetCertificateExact(string domain, [NotNullWhen(true)] out TCert? certificate) =>
        _certificates.TryGetValue(domain, out certificate);

    private bool TryGetWildcardCertificate(string domain, [NotNullWhen(true)] out TCert? certificate)
    {
        if (_wildCardDomains.BinarySearch(new WildCardDomain(domain, null!), DomainNameComparer.Instance) is { } index)
        {
            if (index > -1)
            {
                certificate = _wildCardDomains[index].Certificate!;
                return true;
            }
            // var candidate = _wildCardDomains[~index];
            // if (domain.EndsWith(candidate.Domain, true, CultureInfo.InvariantCulture))
            // {
            //     certificate = candidate.Certificate!;
            //     return true;
            // }
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

        public int Compare(WildCardDomain? x, WildCardDomain? y)
        {
            var ret = Compare(x!.Domain.AsSpan(), y!.Domain.AsSpan());
            if (ret != 0)
            {
                return ret;
            }

            switch (x!.Certificate, y!.Certificate)
            {
                case (null, {}) when x.Domain.Length > y.Domain.Length:
                    return 0;
                case ({}, null) when x.Domain.Length < y.Domain.Length:
                    return 0;
                default:
                    return x.Domain.Length - y.Domain.Length;
            }
        }

        private static int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {

            var length = Math.Min(x.Length, y.Length);

            for (var i = 1; i <= length; i++)
            {
                var charA = x[^i] & 0x5F;
                var charB = y[^i] & 0x5F;

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
