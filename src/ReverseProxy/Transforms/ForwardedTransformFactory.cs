// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms;

internal sealed class ForwardedTransformFactory : ITransformFactory
{
    internal const string XForwardedKey = "X-Forwarded";
    internal const string DefaultXForwardedPrefix = "X-Forwarded-";
    internal const string ForwardedKey = "Forwarded";
    internal const string ActionKey = "Action";
    internal const string HeaderPrefixKey = "HeaderPrefix";
    internal const string ForKey = "For";
    internal const string ByKey = "By";
    internal const string HostKey = "Host";
    internal const string ProtoKey = "Proto";
    internal const string PrefixKey = "Prefix";
    internal const string ForFormatKey = "ForFormat";
    internal const string ByFormatKey = "ByFormat";
    internal const string ClientCertKey = "ClientCert";

    private readonly IRandomFactory _randomFactory;

    public ForwardedTransformFactory(IRandomFactory randomFactory)
    {
        ArgumentNullException.ThrowIfNull(randomFactory);
        _randomFactory = randomFactory;
    }

    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(XForwardedKey, out var headerValue))
        {
            var xExpected = 1;

            ValidateAction(context, XForwardedKey, headerValue);

            if (transformValues.TryGetValue(HeaderPrefixKey, out _))
            {
                xExpected++;
            }

            if (transformValues.TryGetValue(ForKey, out headerValue))
            {
                xExpected++;
                ValidateAction(context, ForKey, headerValue);
            }

            if (transformValues.TryGetValue(PrefixKey, out headerValue))
            {
                xExpected++;
                ValidateAction(context, PrefixKey, headerValue);
            }

            if (transformValues.TryGetValue(HostKey, out headerValue))
            {
                xExpected++;
                ValidateAction(context, HostKey, headerValue);
            }

            if (transformValues.TryGetValue(ProtoKey, out headerValue))
            {
                xExpected++;
                ValidateAction(context, ProtoKey, headerValue);
            }

            TransformHelpers.TryCheckTooManyParameters(context, transformValues, xExpected);
        }
        else if (transformValues.TryGetValue(ForwardedKey, out var forwardedHeader))
        {
            var expected = 1;

            if (transformValues.TryGetValue(ActionKey, out headerValue))
            {
                expected++;
                ValidateAction(context, ForwardedKey + ":" + ActionKey, headerValue);
            }

            var enumValues = "Random,RandomAndPort,Unknown,UnknownAndPort,Ip,IpAndPort";
            if (transformValues.TryGetValue(ForFormatKey, out var forFormat))
            {
                expected++;
                if (!Enum.TryParse<NodeFormat>(forFormat, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ForFormat: {forFormat}. Expected: {enumValues}"));
                }
            }

            if (transformValues.TryGetValue(ByFormatKey, out var byFormat))
            {
                expected++;
                if (!Enum.TryParse<NodeFormat>(byFormat, ignoreCase: true, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ByFormat: {byFormat}. Expected: {enumValues}"));
                }
            }

            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected);

            // for, host, proto, by
            var tokens = forwardedHeader.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                if (!string.Equals(token, ByKey, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for X-Forwarded: {token}. Expected 'for', 'host', 'proto', or 'by'"));
                }
            }
        }
        else if (transformValues.TryGetValue(ClientCertKey, out _))
        {
            TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue(XForwardedKey, out var headerValue))
        {
            var xExpected = 1;

            var defaultXAction = Enum.Parse<ForwardedTransformActions>(headerValue);

            var prefix = DefaultXForwardedPrefix;
            if (transformValues.TryGetValue(HeaderPrefixKey, out var prefixValue))
            {
                xExpected++;
                prefix = prefixValue;
            }

            var xForAction = defaultXAction;
            if (transformValues.TryGetValue(ForKey, out headerValue))
            {
                xExpected++;
                xForAction = Enum.Parse<ForwardedTransformActions>(headerValue);
            }

            var xPrefixAction = defaultXAction;
            if (transformValues.TryGetValue(PrefixKey, out headerValue))
            {
                xExpected++;
                xPrefixAction = Enum.Parse<ForwardedTransformActions>(headerValue);
            }

            var xHostAction = defaultXAction;
            if (transformValues.TryGetValue(HostKey, out headerValue))
            {
                xExpected++;
                xHostAction = Enum.Parse<ForwardedTransformActions>(headerValue);
            }

            var xProtoAction = defaultXAction;
            if (transformValues.TryGetValue(ProtoKey, out headerValue))
            {
                xExpected++;
                xProtoAction = Enum.Parse<ForwardedTransformActions>(headerValue);
            }

            TransformHelpers.CheckTooManyParameters(transformValues, xExpected);

            context.AddXForwardedFor(prefix + ForKey, xForAction);
            context.AddXForwardedPrefix(prefix + PrefixKey, xPrefixAction);
            context.AddXForwardedHost(prefix + HostKey, xHostAction);
            context.AddXForwardedProto(prefix + ProtoKey, xProtoAction);

            if (xForAction != ForwardedTransformActions.Off || xPrefixAction != ForwardedTransformActions.Off
                || xHostAction != ForwardedTransformActions.Off || xProtoAction != ForwardedTransformActions.Off)
            {
                // Remove the Forwarded header when an X-Forwarded transform is enabled
                TransformHelpers.RemoveForwardedHeader(context);
            }
        }
        else if (transformValues.TryGetValue(ForwardedKey, out var forwardedHeader))
        {
            var useHost = false;
            var useProto = false;
            var useFor = false;
            var useBy = false;
            var forFormat = NodeFormat.None;
            var byFormat = NodeFormat.None;

            // for, host, proto, Prefix
            var tokens = forwardedHeader.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                if (string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                {
                    useFor = true;
                    forFormat = NodeFormat.Random; // RFC Default
                }
                else if (string.Equals(token, ByKey, StringComparison.OrdinalIgnoreCase))
                {
                    useBy = true;
                    byFormat = NodeFormat.Random; // RFC Default
                }
                else if (string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase))
                {
                    useHost = true;
                }
                else if (string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase))
                {
                    useProto = true;
                }
                else
                {
                    throw new ArgumentException($"Unexpected value for Forwarded: {token}. Expected 'for', 'host', 'proto', or 'by'");
                }
            }

            var expected = 1;

            var headerAction = ForwardedTransformActions.Set;
            if (transformValues.TryGetValue(ActionKey, out headerValue))
            {
                expected++;
                headerAction = Enum.Parse<ForwardedTransformActions>(headerValue);
            }

            if (useFor && transformValues.TryGetValue(ForFormatKey, out var forFormatString))
            {
                expected++;
                forFormat = Enum.Parse<NodeFormat>(forFormatString, ignoreCase: true);
            }

            if (useBy && transformValues.TryGetValue(ByFormatKey, out var byFormatString))
            {
                expected++;
                byFormat = Enum.Parse<NodeFormat>(byFormatString, ignoreCase: true);
            }

            TransformHelpers.CheckTooManyParameters(transformValues, expected);

            context.UseDefaultForwarders = false;
            if (headerAction != ForwardedTransformActions.Off && (useBy || useFor || useHost || useProto))
            {
                // Not using the extension to avoid resolving the random factory each time.
                context.RequestTransforms.Add(new RequestHeaderForwardedTransform(_randomFactory, forFormat, byFormat, useHost, useProto, headerAction));

                // Remove the X-Forwarded headers when a Forwarded transform is enabled
                TransformHelpers.RemoveAllXForwardedHeaders(context, DefaultXForwardedPrefix);
            }
        }
        else if (transformValues.TryGetValue(ClientCertKey, out var clientCertHeader))
        {
            TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
            context.AddClientCertHeader(clientCertHeader);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static void ValidateAction(TransformRouteValidationContext context, string key, string? headerValue)
    {
        if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
        {
            context.Errors.Add(new ArgumentException($"Unexpected value for {key}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
        }
    }
}
