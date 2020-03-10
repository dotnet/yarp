﻿// <copyright file="IslandGatewayBuilder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.CoreServicesBorrowed;
using Microsoft.Extensions.DependencyInjection;

namespace IslandGateway.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Island Gateway builder for DI configuration.
    /// </summary>
    internal class IslandGatewayBuilder : IIslandGatewayBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IslandGatewayBuilder"/> class.
        /// </summary>
        /// <param name="services">Services collection.</param>
        public IslandGatewayBuilder(IServiceCollection services)
        {
            Contracts.CheckValue(services, nameof(services));
            this.Services = services;
        }

        /// <summary>
        /// Gets the services collection.
        /// </summary>
        public IServiceCollection Services { get; }
    }
}