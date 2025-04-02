// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Connections;

namespace Yarp.Kubernetes.Controller.Certificates;

internal class ServerCertificateSelector : IServerCertificateSelector
{
    private readonly ConcurrentDictionary<NamespacedName, CertificateCandidate> _certificates = new();

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate)
    {
        _certificates[certificateName] = new(certificate);
    }

    public X509Certificate2 GetCertificate(ConnectionContext connectionContext, string domainName)
    {
        foreach (var (cert, domainsMatcher) in _certificates.Values)
        {
            if (domainsMatcher.Any(matcher => matcher.IsMatch(domainName)))
            {
                return cert;
            }
        }

        return _certificates.Values.FirstOrDefault()?.Certificate;
    }

    public void RemoveCertificate(NamespacedName certificateName)
    {
        _certificates.Remove(certificateName, out _);
    }

    private record CertificateCandidate
    {
        public IReadOnlyCollection<Regex> Domains { get; }

        public X509Certificate2 Certificate { get; }

        public CertificateCandidate(X509Certificate2 certificate)
        {
            Certificate = certificate;
            Domains = ImmutableList.CreateRange(GetDomains(certificate).Select(ConvertToRegex));
        }

        private static Regex ConvertToRegex(string domain)
        {
            const RegexOptions options = RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase |
                                         RegexOptions.CultureInvariant;
            if (domain.StartsWith("*."))
            {
                return new Regex($"^.*\\.{Regex.Escape(domain[2..])}$", options);
            }

            return new Regex($"^{Regex.Escape(domain[2..])}$", options);
        }

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

        public void Deconstruct(out X509Certificate2 cert, out IReadOnlyCollection<Regex> domains)
        {
            cert = Certificate;
            domains = Domains;
        }
    }
}
