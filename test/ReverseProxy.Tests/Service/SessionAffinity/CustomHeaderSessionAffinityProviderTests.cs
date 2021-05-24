// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    public class CustomHeaderSessionAffinityProviderTests
    {
        private const string ClusterId = "cluster1";
        private const string AffinityHeaderName = "X-MyAffinity";
        private readonly SessionAffinityConfig _defaultOptions = new SessionAffinityConfig
        {
            Enabled = true,
            Mode = "Cookie",
            FailurePolicy = "Return503",
            AffinityKeyName = AffinityHeaderName
        };
        private readonly IReadOnlyList<DestinationState> _destinations = new[] { new DestinationState("dest-A"), new DestinationState("dest-B"), new DestinationState("dest-C") };

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsNotSetOnRequest_ReturnKeyNotSet()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);

            Assert.Equal(SessionAffinityConstants.Modes.CustomHeader, provider.Mode);

            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, ClusterId, _defaultOptions);

            Assert.Equal(AffinityStatus.AffinityKeyNotSet, affinityResult.Status);
            Assert.Null(affinityResult.Destinations);
        }

        [Fact]
        public void FindAffinitizedDestination_AffinityKeyIsSetOnRequest_Success()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, ClusterId, _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void FindAffinitizedDestination_CustomHeaderNameIsNotSpecified_UseDefaultName()
        {
            var options = new SessionAffinityConfig
            {
                Enabled = true,
                Mode = "CustomHeader",
                FailurePolicy = "Return503"
            };
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[CustomHeaderSessionAffinityProvider.DefaultCustomHeaderName + "_oUB5HSsgqEfyx0xi"] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, ClusterId, options);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);
            Assert.Equal(1, affinityResult.Destinations.Count);
            Assert.Same(affinitizedDestination, affinityResult.Destinations[0]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsNotExtracted_SetKeyOnResponse()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            var chosenDestination = _destinations[1];
            var expectedAffinityHeaderValue = chosenDestination.DestinationId.ToUTF8BytesInBase64();

            provider.AffinitizeRequest(context, _defaultOptions, chosenDestination, ClusterId);

            Assert.True(context.Response.Headers.ContainsKey(AffinityHeaderName));
            Assert.Equal(expectedAffinityHeaderValue, context.Response.Headers[AffinityHeaderName]);
        }

        [Fact]
        public void AffinitizedRequest_AffinityKeyIsExtracted_DoNothing()
        {
            var provider = new CustomHeaderSessionAffinityProvider(AffinityTestHelper.GetDataProtector().Object, AffinityTestHelper.GetLogger<CustomHeaderSessionAffinityProvider>().Object);
            var context = new DefaultHttpContext();
            context.Request.Headers["SomeHeader"] = new[] { "SomeValue" };
            var affinitizedDestination = _destinations[1];
            context.Request.Headers[AffinityHeaderName] = new[] { affinitizedDestination.DestinationId.ToUTF8BytesInBase64() };

            var affinityResult = provider.FindAffinitizedDestinations(context, _destinations, ClusterId, _defaultOptions);

            Assert.Equal(AffinityStatus.OK, affinityResult.Status);

            provider.AffinitizeRequest(context, _defaultOptions, affinitizedDestination, ClusterId);

            Assert.False(context.Response.Headers.ContainsKey(AffinityHeaderName));
        }
    }
}
