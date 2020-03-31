﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Management.Tests
{
    /// <summary>
    /// Tests for the <see cref="RouteManager"/> class.
    /// Additional scenarios are covered in <see cref="ItemManagerBaseTests"/>.
    /// </summary>
    public class RouteManagerTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new RouteManager();
        }

        [Fact]
        public void GetOrCreateItem_NonExistentItem_CreatesNewItem()
        {
            // Arrange
            var manager = new RouteManager();

            // Act
            var item = manager.GetOrCreateItem("abc", item => { });

            // Assert
            item.Should().NotBeNull();
            item.RouteId.Should().Be("abc");
        }
    }
}
