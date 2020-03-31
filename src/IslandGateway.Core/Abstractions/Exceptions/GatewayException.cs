﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Represents unexpected gateway errors.
    /// </summary>
    public sealed class GatewayException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayException"/> class.
        /// </summary>
        public GatewayException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayException"/> class.
        /// </summary>
        public GatewayException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayException"/> class.
        /// </summary>
        public GatewayException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
