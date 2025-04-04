// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Yarp.Kubernetes.Controller.Certificates;

internal partial class ServerCertificateSelector
{
    private class CertificateStore
    {
        private readonly List<WildCardDomain> _wildCardDomains = new();
        private readonly Dictionary<string, X509Certificate2> _certificates = new(StringComparer.OrdinalIgnoreCase);

        public CertificateStore(IEnumerable<X509Certificate2> certificates)
        {

            foreach (var certificate in certificates)
            {
                foreach (var domain in GetDomains(certificate))
                {
                    if (domain.StartsWith("*."))
                    {
                        _wildCardDomains.Add(new (domain[2..], certificate));
                    }
                    else
                    {
                        _certificates[domain] = certificate;
                    }
                }
            }

            _wildCardDomains.Sort(DomainNameComparer.Instance);
        }


        public X509Certificate2 GetCertificate(string domain)
        {
            // First search for exact match for certificate.
            if (_certificates.TryGetValue(domain, out var cert))
            {
                return cert;
            }


            // By using a binary search, we can achieve O(log n) suffix search whilst avoiding a complex
            // tree/trie structure in the heap.
            if (_wildCardDomains.BinarySearch(new WildCardDomain(domain, null!), DomainNameComparer.Instance) is { } index and < -1)
            {
                var candidate = _wildCardDomains[~index];
                if (domain.EndsWith(candidate.Domain, true, CultureInfo.InvariantCulture))
                {
                    return candidate.Certificate;
                }
            }

            return _wildCardDomains.FirstOrDefault()?.Certificate
                   ?? _certificates.Values.FirstOrDefault();
        }
    }

    private record WildCardDomain(string Domain, X509Certificate2 Certificate);

    private static IEnumerable<string> GetDomains(X509Certificate2 certificate)
    {
        if (certificate.GetNameInfo(X509NameType.DnsName, false) is { } dnsName)
        {
            yield return dnsName;
        }

        const string SAN_OID = "2.5.29.17";
        var extension = certificate.Extensions[SAN_OID];
        if (extension is null)
        {
            yield break;
        }

        var dnsNameTag = new Asn1Tag(TagClass.ContextSpecific, tagValue: 2, isConstructed: false);

        var asnReader = new AsnReader(extension.RawData, AsnEncodingRules.BER);
        var sequenceReader = asnReader.ReadSequence(Asn1Tag.Sequence);
        while (sequenceReader.HasData)
        {
            var tag = sequenceReader.PeekTag();
            if (tag != dnsNameTag)
            {
                sequenceReader.ReadEncodedValue();
                continue;
            }

            var alternativeName = sequenceReader.ReadCharacterString(UniversalTagNumber.IA5String, dnsNameTag);
            yield return alternativeName;
        }

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
            return Compare(x!.Domain.AsSpan(), y!.Domain.AsSpan());
        }

        private static int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {

            var length = Math.Min(x.Length, y.Length);

            for (var i = 1; i <= length; i++)
            {
                var charA = x[^i] & 0x3F;
                var charB = y[^i] & 0x3F;

                if (charA == charB)
                {
                    continue;
                }

                return charB - charA;
            }

            return x.Length - y.Length;
        }

    }
}
