// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.ReverseProxy.Delegation;

// Only used as part of a workaround for https://github.com/dotnet/aspnetcore/issues/59166.
internal sealed class DummyHttpSysDelegator : IHttpSysDelegator
{
    public void ResetQueue(string queueName, string urlPrefix) { }
}
