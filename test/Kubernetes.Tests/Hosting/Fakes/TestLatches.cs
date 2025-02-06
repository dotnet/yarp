// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Yarp.Kubernetes.Tests.Hosting.Fakes;

public class TestLatches
{
    public TestLatch RunEnter { get; } = new TestLatch();
    public TestLatch RunResult { get; } = new TestLatch();
    public TestLatch RunExit { get; } = new TestLatch();
}
