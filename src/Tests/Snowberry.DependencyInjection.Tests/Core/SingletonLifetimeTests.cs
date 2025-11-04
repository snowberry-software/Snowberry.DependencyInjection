using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for singleton service lifetime behavior including instance reuse,
/// factory methods, and disposal patterns.
/// </summary>
public class SingletonLifetimeTests
{
    [Fact]
    public void GetService_WithSingletonRegistration_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithSingletonInstance_ShouldReturnProvidedInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        var providedInstance = new TestService { Name = "ProvidedInstance" };
        container.RegisterSingleton<ITestService>(providedInstance);

        // Act
        var retrievedService = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(providedInstance, retrievedService);
        Assert.Equal("ProvidedInstance", retrievedService.Name);
        Assert.Equal(0, container.DisposableContainer.DisposableCount); // User-provided instances are not tracked for disposal
    }

    [Fact]
    public void GetService_WithSingletonFactory_ShouldUseSameFactoryInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        int callCount = 0;
        container.RegisterSingleton<ITestService>((sp, key) =>
        {
            callCount++;
            return new TestService { Name = $"Factory_{callCount}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal("Factory_1", service1.Name);
        Assert.Equal(1, callCount); // Factory should only be called once
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithSingletonConcreteType_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<TestService>();

        // Act
        var service1 = container.GetRequiredService<TestService>();
        var service2 = container.GetRequiredService<TestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void DisposeContainer_WithSingletonServices_ShouldDisposeAllServices()
    {
        // Arrange
        TestService service1, service2;
        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterSingleton<TestService>();

            service1 = (TestService)container.GetRequiredService<ITestService>();
            service2 = container.GetRequiredService<TestService>();

            Assert.Equal(2, container.DisposableContainer.DisposableCount);
        }

        // Assert
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
    }

    [Fact]
    public void GetService_WithSingletonAndCustomFactory_ShouldPassCorrectParameters()
    {
        // Arrange
        using var container = new ServiceContainer();
        object? capturedServiceKey = null;

        container.RegisterSingleton<ITestService>((provider, serviceKey) =>
        {
            capturedServiceKey = serviceKey;
            return new TestService { Name = "FactoryTest" };
        });

        // Act
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Null(capturedServiceKey);
        Assert.Equal("FactoryTest", service.Name);
    }
}