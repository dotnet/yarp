// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Yarp.Kubernetes.Controller.Certificates;

internal class ServerCertificateSelector : IServerCertificateSelector
{
    private readonly Dictionary<NamespacedName, CertificateCandidate> _certificates = new();
    private readonly ReaderWriterLock _readerWriterLock = new();

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate)
    {
        _readerWriterLock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            _certificates[certificateName] = new(certificate);
        }
        finally
        {
            _readerWriterLock.ReleaseWriterLock();
        }
    }

    public X509Certificate2 GetCertificate(ConnectionContext connectionContext, string domainName)
    {
        _readerWriterLock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            foreach (var (cert, domains) in _certificates.Values)
            {
                if (domains.Contains(domainName))
                {
                    return cert;
                }
            }
        }
        finally
        {
            _readerWriterLock.ReleaseReaderLock();
        }

        return _certificates.Values.FirstOrDefault()?.Certificate;
    }

    public void RemoveCertificate(NamespacedName certificateName)
    {
        _readerWriterLock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            _certificates.Remove(certificateName);
        }
        finally
        {
            _readerWriterLock.ReleaseWriterLock();
        }
    }

    private record CertificateCandidate
    {
        public IReadOnlySet<string> Domains { get; }

        public X509Certificate2 Certificate { get; }

        public CertificateCandidate(X509Certificate2 certificate)
        {
            Certificate = certificate;
            Domains = ImmutableHashSet.CreateRange(StringComparer.InvariantCultureIgnoreCase, GetDomains(certificate));
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

        public void Deconstruct(out X509Certificate2 cert, out IReadOnlySet<string> domains)
        {
            cert = Certificate;
            domains = Domains;
        }
    }
}
