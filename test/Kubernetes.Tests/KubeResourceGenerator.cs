// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using k8s.Models;

namespace Yarp.Kubernetes.Tests;

internal static class KubeResourceGenerator
{
    public static V1IngressClass CreateIngressClass(string name, string controller, bool? isDefaultClass)
    {
        var ingressClass = new V1IngressClass
        {
            Spec = new V1IngressClassSpec
            {
                Controller = controller,
            },
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Annotations = new Dictionary<string, string>(),
            },
        };

        if (isDefaultClass.HasValue && isDefaultClass.Value)
        {
            ingressClass.Metadata.Annotations.Add("ingressclass.kubernetes.io/is-default-class", isDefaultClass.Value.ToString(CultureInfo.InvariantCulture));
        }

        return ingressClass;
    }

    public static V1Ingress CreateIngress(string name, string namespaceName, string ingressClassName)
    {
        var ingress = new V1Ingress
        {
            Spec = new V1IngressSpec(),
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = namespaceName,
            }
        };

        if (!string.IsNullOrEmpty(ingressClassName))
        {
            ingress.Spec.IngressClassName = ingressClassName;
        }

        return ingress;
    }

    public static V1Secret CreateSecret(string name, string namespaceName, byte[] publicData = null, byte[] privateData = null)
    {
        var secret = new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = namespaceName,
            }
        };

        if (publicData != null)
        {
            if (secret.Data == null)
            {
                secret.Data = new Dictionary<string, byte[]>();
            }

            secret.Data["tls.crt"] = publicData;
        }

        if (privateData != null)
        {
            if (secret.Data == null)
            {
                secret.Data = new Dictionary<string, byte[]>();
            }

            secret.Data["tls.key"] = privateData;
        }

        return secret;
    }
}
