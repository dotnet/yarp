// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Application.Configuration;

public sealed class HttpsOptions
{
    public bool Redirect { get; set; }
    public bool Hsts { get; set; }
}
