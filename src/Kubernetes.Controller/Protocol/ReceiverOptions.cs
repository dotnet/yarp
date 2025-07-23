// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;

namespace Yarp.Kubernetes.Protocol;

public class ReceiverOptions
{
    public Uri ControllerUrl { get; set; }

    public HttpMessageInvoker Client { get; set; }
}
