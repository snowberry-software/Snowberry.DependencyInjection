using System.Collections.Concurrent;
using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

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
        var service = container.GetService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetService_WithUnregisteredService_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        var exception = Assert.Throws<ServiceTypeNotRegistered>(container.GetService<ITestService>);

        Assert.Contains(typeof(ITestService).Name, exception.Message);
    }

    [Fact]
    public void GetOptionalService_WithRegisteredService_ShouldReturnInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        var service = container.GetOptionalService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalService_WithUnregisteredService_ShouldReturnNull()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var service = container.GetOptionalService<ITestService>();

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
        Assert.Throws<ObjectDisposedException>(container.GetService<ITestService>);
    }

    [Fact]
    public void GetOptionalService_FromDisposedContainer_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(container.GetOptionalService<ITestService>);
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

    [Fact]
    public void IsServiceRegistered_WithNullKey_ShouldCheckDefaultRegistration()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool isRegistered = container.IsServiceRegistered<ITestService>(null);

        // Assert
        Assert.True(isRegistered);
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
            ? container.GetService<ITestService>()
            : container.GetKeyedService<ITestService>(serviceKey);

        // Assert
        Assert.NotNull(service);
        Assert.Equal(expectedName, service.Name);
    }

    [Fact]
    public void GetService_MultipleThreads_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        var services = new ConcurrentBag<ITestService>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var service = container.GetService<ITestService>();
                services.Add(service);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(10, services.Count);
        var distinctServices = services.Distinct().ToList();
        Assert.Single(distinctServices); // All should be the same singleton instance
    }

    [Fact]
    public void GetService_WithTypeParameter_ShouldResolveCorrectly()
    {
        // Tests the GetService(Type) overload using ServiceDescriptor for non-generic registration

        // Arrange
        using var container = new ServiceContainer();
        var descriptor = ServiceDescriptor.Singleton(typeof(ITestService), typeof(TestService), null);
        container.Register(descriptor);

        // Act
        object? service = container.GetService(typeof(ITestService));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalService_WithTypeParameter_ShouldResolveCorrectly()
    {
        // Tests the GetOptionalService(Type) overload using ServiceDescriptor for non-generic registration

        // Arrange
        using var container = new ServiceContainer();
        var descriptor = ServiceDescriptor.Singleton(typeof(ITestService), typeof(TestService), null);
        container.Register(descriptor);

        // Act
        object? service = container.GetOptionalService(typeof(ITestService));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalService_WithTypeParameter_AndUnregisteredService_ShouldReturnNull()
    {
        // Tests the GetOptionalService(Type) overload with unregistered service

        // Arrange
        using var container = new ServiceContainer();

        // Act
        object? service = container.GetOptionalService(typeof(ITestService));

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetService_WithOpenGenericType_ShouldResolveClosedGeneric()
    {
        // Tests actual generic type parameter resolution with open generics

        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IRepository<>), typeof(Repository<>), null, ServiceLifetime.Singleton, null);

        // Act
        var stringRepo = container.GetService<IRepository<string>>();
        var intRepo = container.GetService<IRepository<int>>();

        // Assert
        Assert.NotNull(stringRepo);
        Assert.NotNull(intRepo);
        Assert.IsType<Repository<string>>(stringRepo);
        Assert.IsType<Repository<int>>(intRepo);
        Assert.NotSame(stringRepo, intRepo); // Different generic types should be different instances
    }

    [Fact]
    public void GetService_WithComplexGenericType_ShouldResolveCorrectly()
    {
        // Tests complex generic type resolution

        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IRepository<>), typeof(Repository<>), null, ServiceLifetime.Singleton, null);

        // Act
        var listRepo = container.GetService<IRepository<List<string>>>();
        var dictRepo = container.GetService<IRepository<Dictionary<string, int>>>();

        // Assert
        Assert.NotNull(listRepo);
        Assert.NotNull(dictRepo);
        Assert.IsType<Repository<List<string>>>(listRepo);
        Assert.IsType<Repository<Dictionary<string, int>>>(dictRepo);
    }
}