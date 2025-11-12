// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Yarp.Kubernetes.Controller.Certificates;

internal partial class ServerCertificateSelector
{
    private class ImmutableX509CertificateCache(IEnumerable<X509Certificate2> certificates)
        : ImmutableCertificateCache<X509Certificate2>(certificates, GetDomains)
    {
        protected override X509Certificate2? GetDefaultCertificate()
        {
            if (WildcardCertificates.Count != 0)
            {
                return WildcardCertificates[0].Certificate;
            }
            return Certificates.Values.FirstOrDefault();
        }

        public X509Certificate2? DefaultCertificate()
        {
            return Certificates.Values.FirstOrDefault();
        }
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



}
