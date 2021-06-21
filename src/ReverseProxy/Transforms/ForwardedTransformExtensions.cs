// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Extensions for adding forwarded header transforms.
    /// </summary>
    public static class ForwardedTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will add X-Forwarded-* headers.
        /// </summary>
        public static RouteConfig WithTransformXForwarded(this RouteConfig route, string headerPrefix = "X-Forwarded-", bool useFor = true,
            bool useHost = true, bool useProto = true, bool usePrefix = true, ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            var actionString = action.ToString();
            return route.WithTransform(transform =>
            {
                if (useFor)
                {
                    transform[headerPrefix + ForwardedTransformFactory.ForKey] = action.ToString();
                }

                if (usePrefix)
                {
                    transform[headerPrefix + ForwardedTransformFactory.PrefixKey] = action.ToString();
                }

                if (useHost)
                {
                    transform[headerPrefix + ForwardedTransformFactory.HostKey] = action.ToString();
                }

                if (useProto)
                {
                    transform[headerPrefix + ForwardedTransformFactory.ProtoKey] = action.ToString();
                }

                transform[ForwardedTransformFactory.PrefixForwardedKey] = headerPrefix;
            });
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-For request header.
        /// </summary>
        public static TransformBuilderContext AddXForwardedFor(this TransformBuilderContext context, string headerName = "X-Forwarded-For", ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.UseDefaultForwarders = false;
            if (action == ForwardedTransformActions.Off)
            {
                return context;
            }
            context.RequestTransforms.Add(new RequestHeaderXForwardedForTransform(headerName, action));
            return context;
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-Host request header.
        /// </summary>
        public static TransformBuilderContext AddXForwardedHost(this TransformBuilderContext context, string headerName = "X-Forwarded-Host", ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.UseDefaultForwarders = false;
            if (action == ForwardedTransformActions.Off)
            {
                return context;
            }
            context.RequestTransforms.Add(new RequestHeaderXForwardedHostTransform(headerName, action));
            return context;
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-Proto request header.
        /// </summary>
        public static TransformBuilderContext AddXForwardedProto(this TransformBuilderContext context, string headerName = "X-Forwarded-Proto", ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.UseDefaultForwarders = false;
            if (action == ForwardedTransformActions.Off)
            {
                return context;
            }
            context.RequestTransforms.Add(new RequestHeaderXForwardedProtoTransform(headerName, action));
            return context;
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-Proto request header.
        /// </summary>
        public static TransformBuilderContext AddXForwardedPrefix(this TransformBuilderContext context, string headerName = "X-Forwarded-Prefix", ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.UseDefaultForwarders = false;
            if (action == ForwardedTransformActions.Off)
            {
                return context;
            }
            context.RequestTransforms.Add(new RequestHeaderXForwardedPrefixTransform(headerName, action));
            return context;
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-* request headers.
        /// </summary>
        public static TransformBuilderContext AddXForwarded(this TransformBuilderContext context, ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.AddXForwardedFor(action: action);
            context.AddXForwardedPrefix(action: action);
            context.AddXForwardedHost(action: action);
            context.AddXForwardedProto(action: action);
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static RouteConfig WithTransformForwarded(this RouteConfig route, bool useHost = true, bool useProto = true,
            NodeFormat forFormat = NodeFormat.Random, NodeFormat byFormat = NodeFormat.Random, ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            var headers = new List<string>();

            if (forFormat != NodeFormat.None)
            {
                headers.Add(ForwardedTransformFactory.ForKey);
            }

            if (byFormat != NodeFormat.None)
            {
                headers.Add(ForwardedTransformFactory.ByKey);
            }

            if (useHost)
            {
                headers.Add(ForwardedTransformFactory.HostKey);
            }

            if (useProto)
            {
                headers.Add(ForwardedTransformFactory.ProtoKey);
            }

            return route.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.ForwardedKey] = string.Join(',', headers);
                transform[ForwardedTransformFactory.ActionKey] = action.ToString();

                if (forFormat != NodeFormat.None)
                {
                    transform.Add(ForwardedTransformFactory.ForFormatKey, forFormat.ToString());
                }

                if (byFormat != NodeFormat.None)
                {
                    transform.Add(ForwardedTransformFactory.ByFormatKey, byFormat.ToString());
                }
            });
        }

        /// <summary>
        /// Adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static TransformBuilderContext AddForwarded(this TransformBuilderContext context,
            bool useHost = true, bool useProto = true, NodeFormat forFormat = NodeFormat.Random,
            NodeFormat byFormat = NodeFormat.Random, ForwardedTransformActions action = ForwardedTransformActions.Set)
        {
            context.UseDefaultForwarders = false;
            if (byFormat != NodeFormat.None || forFormat != NodeFormat.None || useHost || useProto)
            {
                var random = context.Services.GetRequiredService<IRandomFactory>();
                context.RequestTransforms.Add(new RequestHeaderForwardedTransform(random,
                    forFormat, byFormat, useHost, useProto, action));
            }
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will set the given header with the Base64 encoded client certificate.
        /// </summary>
        public static RouteConfig WithTransformClientCertHeader(this RouteConfig route, string headerName)
        {
            return route.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.ClientCertKey] = headerName;
            });
        }

        /// <summary>
        /// Adds the transform which will set the given header with the Base64 encoded client certificate.
        /// </summary>
        public static TransformBuilderContext AddClientCertHeader(this TransformBuilderContext context, string headerName)
        {
            context.RequestTransforms.Add(new RequestHeaderClientCertTransform(headerName));
            return context;
        }
    }
}
