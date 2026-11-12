using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Tests for service resolution functionality including required services,
/// optional services, and error handling scenarios.
/// </summary>
public class ServiceResolutionTests
{
    [Fact]
    public void GetService_WithRegisteredService_ShouldReturnInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetService_WithUnregisteredService_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        var exception = Assert.Throws<ServiceTypeNotRegistered>(container.GetRequiredService<ITestService>);

        Assert.Contains(typeof(ITestService).Name, exception.Message);
    }

    [Fact]
    public void GetOptionalService_WithRegisteredService_ShouldReturnInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        var service = container.GetService<ITestService>();

        // Assert
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalService_WithUnregisteredService_ShouldReturnNull()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var service = container.GetService<ITestService>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetService_FromDisposedContainer_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(container.GetRequiredService<ITestService>);
    }

    [Fact]
    public void GetOptionalService_FromDisposedContainer_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(container.GetService<ITestService>);
    }

    [Fact]
    public void IsServiceRegistered_WithRegisteredService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool isRegistered = container.IsServiceRegistered<ITestService>(null);

        // Assert
        Assert.True(isRegistered);
    }

    [Fact]
    public void IsServiceRegistered_WithUnregisteredService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool isRegistered = container.IsServiceRegistered<ITestService>(null);

        // Assert
        Assert.False(isRegistered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("testKey")]
    public void GetService_WithDifferentKeyTypes_ShouldResolveCorrectly(string? serviceKey)
    {
        // Arrange
        using var container = new ServiceContainer();
        string expectedName = serviceKey ?? "default";

        if (serviceKey == null)
        {
            container.RegisterSingleton<ITestService>(new TestService { Name = expectedName });
        }
        else
        {
            container.RegisterSingleton<ITestService>(new TestService { Name = expectedName }, serviceKey);
        }

        // Act
        var service = serviceKey == null
            ? container.GetRequiredService<ITestService>()
            : container.GetKeyedService<ITestService>(serviceKey);

        // Assert
        Assert.NotNull(service);
        Assert.Equal(expectedName, service.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetService_WithTypeParameter_ShouldResolveCorrectly(bool required)
    {
        // Tests the GetRequiredService(Type) and GetService(Type) overloads using ServiceDescriptor for non-generic registration

        // Arrange
        using var container = new ServiceContainer();
        var descriptor = ServiceDescriptor.Singleton(typeof(ITestService), typeof(TestService), null);
        container.Register(descriptor);

        // Act
        object? service = required
            ? container.GetRequiredService(typeof(ITestService))
            : container.GetService(typeof(ITestService));

        // Assert
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalService_WithTypeParameter_AndUnregisteredService_ShouldReturnNull()
    {
        // Tests the GetOptionalService(Type) overload with unregistered service

        // Arrange
        using var container = new ServiceContainer();

        // Act
        object? service = container.GetService(typeof(ITestService));

        // Assert
        Assert.Null(service);
    }
}
