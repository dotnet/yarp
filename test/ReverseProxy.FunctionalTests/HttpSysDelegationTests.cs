// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Delegation;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy;

public partial class HttpSysDelegationTests
{
    [HttpSysDelegationFact]
    public async Task RequestDelegated()
    {
        IHttpSysDelegator delegator = null;
        IForwarderErrorFeature proxyError = null;
        Exception unhandledError = null;
        var expectedRepsone = "Hello World!";
        var queueName = nameof(HttpSysDelegationTests) + Random.Shared.Next().ToString("x8");
        string urlPrefix = null;

        var test = new HttpSysTestEnvironment(
            destinationServices => { },
            destinationHttpSysOptions => destinationHttpSysOptions.RequestQueueName = queueName,
            destinationApp => destinationApp.Run(context => context.Response.WriteAsync(expectedRepsone)),
            proxyServices => { },
            proxyBuilder => { },
            proxyApp =>
            {
                delegator = proxyApp.ApplicationServices.GetRequiredService<IHttpSysDelegator>();
                proxyApp.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                        proxyError = context.Features.Get<IForwarderErrorFeature>();
                    }
                    catch (Exception ex)
                    {
                        unhandledError = ex;
                        throw;
                    }
                });
            },
            proxyPipeline =>
            {
                proxyPipeline.UseHttpSysDelegation();
            },
            (cluster, route) =>
            {
                urlPrefix = cluster.Destinations.First().Value.Address;
                var destination = new DestinationConfig()
                {
                    Address = urlPrefix,
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { DelegationExtensions.HttpSysDelegationQueueMetadataKey, queueName },
                    },
                };

                cluster = new ClusterConfig
                {
                    ClusterId = cluster.ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1",  destination },
                    },
                };

                return (cluster, route);
            });

        await test.Invoke(async proxyUri =>
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync(proxyUri);

            Assert.Null(proxyError);
            Assert.Null(unhandledError);
            Assert.Equal(expectedRepsone, response);

            Assert.NotNull(delegator);
            delegator.ResetQueue(queueName, urlPrefix);

            response = await httpClient.GetStringAsync(proxyUri);

            Assert.Null(proxyError);
            Assert.Null(unhandledError);
            Assert.Equal(expectedRepsone, response);
        });
    }

    private class HttpSysDelegationFactAttribute : FactAttribute
    {
        public HttpSysDelegationFactAttribute([CallerFilePath] string sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            // Htty.sys delegation was added to Windows in the 21H2 release but back ported through RS5 (1809)
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 1809))
            {
                Skip = "Http.sys tests are only supported on Windows versions >= 10.0.1809";
            }
        }
    }
}
