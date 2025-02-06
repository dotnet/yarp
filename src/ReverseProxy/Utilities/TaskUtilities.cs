// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Utilities;

internal static class TaskUtilities
{
    internal static readonly Task<bool> TrueTask = Task.FromResult(true);
    internal static readonly Task<bool> FalseTask = Task.FromResult(false);
}
