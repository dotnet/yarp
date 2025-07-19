// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Autofac.Core;
using Autofac.Extras.Moq;
using Moq;

namespace Yarp.Tests.Common;

/// <summary>
/// Automatically generates mocks for interfaces on the Class under test.
/// </summary>
public class TestAutoMockBase : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAutoMockBase" /> class.
    /// </summary>
    public TestAutoMockBase()
    {
        AutoMock = AutoMock.GetLoose();
    }

    /// <summary>
    /// Gets the mocks.
    /// </summary>
    protected AutoMock AutoMock { get; private set; }

    /// <summary>
    /// Resets the mocks.
    /// </summary>
    public void ResetMocks()
    {
        AutoMock.Dispose();
        AutoMock = AutoMock.GetLoose();
    }

    /// <summary>
    /// Creates an object of <typeparamref name="TService"/> using the dependency container.
    /// </summary>
    /// <typeparam name="TService">The type of the object to create.</typeparam>
    /// <param name="parameters">The parameters.</param>
    /// <returns>
    /// Instance of <typeparamref name="TService"/>.
    /// </returns>
    public virtual TService Create<TService>(params Parameter[] parameters)
        where TService : class
    {
        return AutoMock.Create<TService>(parameters);
    }

    /// <summary>
    /// Creates a mock of the specified abstraction.
    /// </summary>
    /// <typeparam name="TDependencyToMock">The type of the dependency to mock.</typeparam>
    /// <returns>A mock of the type.</returns>
    public Mock<TDependencyToMock> Mock<TDependencyToMock>()
        where TDependencyToMock : class
    {
        return AutoMock.Mock<TDependencyToMock>();
    }

    /// <summary>
    /// Provide the specified abstraction to the dependency injection container.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="instance">The instance.</param>
    public void Provide<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        AutoMock.Provide(instance);
    }

    /// <summary>
    /// Provide the specified concrete type to the dependency injection container.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <typeparam name="TImplementation">The type that implements the service.</typeparam>
    /// <returns>An instance of <typeparamref name="TImplementation"/> that implements <typeparamref name="TService"/>.</returns>
    public TImplementation Provide<TService, TImplementation>()
        where TService : class
        where TImplementation : TService
    {
        return (TImplementation)AutoMock.Provide<TService, TImplementation>();
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (!_isDisposed)
        {
            AutoMock.Dispose();
            _isDisposed = true;
        }
    }
}
