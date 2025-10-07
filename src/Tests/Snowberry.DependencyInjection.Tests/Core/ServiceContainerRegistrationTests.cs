using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for service container registration functionality including service registration,
/// overwriting, and registration options validation.
/// </summary>
public class ServiceContainerRegistrationTests
{
    [Fact]
    public void RegisterService_WithValidTypeMapping_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        container.RegisterSingleton<ITestService, TestService>();

        // Assert
        Assert.Equal(1, container.Count);
        Assert.True(container.IsServiceRegistered<ITestService>(null));
    }

    [Fact]
    public void RegisterService_WithSameInterface_ShouldOverwriteCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
        var firstInstance = new TestService { Name = "First" };
        var secondInstance = new TestService { Name = "Second" };

        // Act
        container.RegisterSingleton<ITestService>(firstInstance);
        container.RegisterSingleton<ITestService>(secondInstance);

        // Assert
        var service = container.GetRequiredService<ITestService>();
        Assert.Equal("Second", service.Name);
        Assert.Equal(1, container.Count);
    }

    [Fact]
    public void RegisterService_InReadOnlyContainer_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ReadOnly);
        container.RegisterSingleton<ITestService, TestService>();

        // Act & Assert
        Assert.Throws<ServiceRegistryReadOnlyException>(() =>
            container.RegisterSingleton<ITestService, TestService>());
    }

    [Fact]
    public void RegisterService_WithConcreteTypeOnly_ShouldRegisterSelfType()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        container.RegisterSingleton<TestService>();

        // Assert
        Assert.Equal(1, container.Count);
        var service = container.GetRequiredService<TestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void RegisterService_WithInvalidImplementationType_ShouldThrowOnResolution()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        container.RegisterSingleton<ITestService>();

        // Assert
        Assert.Throws<InvalidServiceImplementationType>(container.GetRequiredService<ITestService>);
    }

    [Fact]
    public void UnregisterService_WithRegisteredService_ShouldRemoveAndDisposeService()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
        var instance = new TestService();
        container.RegisterSingleton<ITestService>(instance);

        // Act
        container.UnregisterService<ITestService>(null, out bool successful);

        // Assert
        Assert.True(successful);
        Assert.False(container.IsServiceRegistered<ITestService>(null));
        Assert.True(instance.IsDisposed);
    }

    [Fact]
    public void GetServiceDescriptors_WithMultipleServices_ShouldReturnAllDescriptors()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<TestService>();

        // Act
        var descriptors = container.GetServiceDescriptors();

        // Assert
        Assert.Equal(2, descriptors.Length);
        Assert.Equal(2, container.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryAddService_WithExistingService_ShouldReturnExpectedResult(bool serviceExists)
    {
        // Arrange
        using var container = new ServiceContainer();
        if (serviceExists)
        {
            container.RegisterSingleton<ITestService, TestService>();
        }

        // Act
        bool result = container.TryRegister(new ServiceDescriptor(
            typeof(ITestService),
            typeof(TestService),
            ServiceLifetime.Singleton));

        // Assert
        Assert.Equal(!serviceExists, result);
    }
}