using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for keyed service registration and resolution including different service keys,
/// lifetime management, and dependency injection with keyed services.
/// </summary>
public class KeyedServiceTests
{
    [Fact]
    public void RegisterKeyedService_WithStringKey_ShouldResolveCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "testKey";
        var instance = new TestService { Name = "KeyedService" };

        // Act
        container.RegisterSingleton<ITestService>(instance, serviceKey);
        var resolvedService = container.GetKeyedService<ITestService>(serviceKey);

        // Assert
        Assert.Same(instance, resolvedService);
        Assert.Equal("KeyedService", resolvedService.Name);
    }

    [Fact]
    public void RegisterKeyedService_WithMultipleKeys_ShouldIsolateServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string key1 = "key1";
        const string key2 = "key2";

        var service1 = new TestService { Name = "Service1" };
        var service2 = new TestService { Name = "Service2" };

        // Act
        container.RegisterSingleton<ITestService>(service1, key1);
        container.RegisterSingleton<ITestService>(service2, key2);

        var resolved1 = container.GetKeyedService<ITestService>(key1);
        var resolved2 = container.GetKeyedService<ITestService>(key2);

        // Assert
        Assert.Same(service1, resolved1);
        Assert.Same(service2, resolved2);
        Assert.NotSame(resolved1, resolved2);
        Assert.Equal("Service1", resolved1.Name);
        Assert.Equal("Service2", resolved2.Name);
        Assert.Equal(2, container.Count);
    }

    [Fact]
    public void RegisterKeyedService_WithSameKeyDifferentTypes_ShouldCoexist()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string sharedKey = "sharedKey";

        // Act
        container.RegisterSingleton<ITestService, TestService>(sharedKey);
        container.RegisterSingleton<TestService>(sharedKey);

        var testService = container.GetKeyedService<ITestService>(sharedKey);
        var concreteService = container.GetKeyedService<TestService>(sharedKey);

        // Assert
        Assert.NotNull(testService);
        Assert.NotNull(concreteService);
        Assert.NotSame(testService, concreteService);
        Assert.Equal(2, container.Count);
    }

    [Theory]
    [InlineData("stringKey")]
    [InlineData(42)]
    [InlineData(true)]
    public void RegisterKeyedService_WithDifferentKeyTypes_ShouldWork<T>(T serviceKey)
    {
        // Arrange
        using var container = new ServiceContainer();
        var service = new TestService { Name = serviceKey?.ToString() };

        // Act
        container.RegisterSingleton<ITestService>(service, serviceKey);
        var resolvedService = container.GetKeyedService<ITestService>(serviceKey);

        // Assert
        Assert.Same(service, resolvedService);
        Assert.Equal(serviceKey?.ToString(), resolvedService.Name);
    }

    [Fact]
    public void RegisterKeyedService_WithFactory_ShouldPassCorrectKey()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string expectedKey = "factoryKey";
        object? capturedKey = null;

        // Act
        container.RegisterSingleton<ITestService>((provider, key) =>
        {
            capturedKey = key;
            return new TestService { Name = key?.ToString() };
        }, expectedKey);

        var service = container.GetKeyedService<ITestService>(expectedKey);

        // Assert
        Assert.Equal(expectedKey, capturedKey);
        Assert.Equal(expectedKey, service.Name);
    }

    [Fact]
    public void GetKeyedService_WithUnregisteredKey_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>("existingKey");

        // Act & Assert
        Assert.Throws<Snowberry.DependencyInjection.Abstractions.Exceptions.ServiceTypeNotRegistered>(() =>
            container.GetKeyedService<ITestService>("nonExistentKey"));
    }

    [Fact]
    public void GetOptionalKeyedService_WithRegisteredKey_ShouldReturnService()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "testKey";
        container.RegisterSingleton<ITestService, TestService>(serviceKey);

        // Act
        var service = container.GetOptionalKeyedService<ITestService>(serviceKey);

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void GetOptionalKeyedService_WithUnregisteredKey_ShouldReturnNull()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var service = container.GetOptionalKeyedService<ITestService>("nonExistentKey");

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void KeyedServices_WithTransientLifetime_ShouldCreateNewInstancesPerKey()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string key1 = "transient1";
        const string key2 = "transient2";

        container.RegisterTransient<ITestService, TestService>(key1);
        container.RegisterTransient<ITestService, TestService>(key2);

        // Act
        var service1a = container.GetKeyedService<ITestService>(key1);
        var service1b = container.GetKeyedService<ITestService>(key1);
        var service2a = container.GetKeyedService<ITestService>(key2);

        // Assert
        Assert.NotSame(service1a, service1b); // Different instances for same key
        Assert.NotSame(service1a, service2a); // Different instances for different keys
        Assert.Equal(3, container.DisposableCount);
    }

    [Fact]
    public void KeyedServices_WithScopedLifetime_ShouldRespectScopeBoundaries()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "scopedKey";
        container.RegisterScoped<ITestService, TestService>(serviceKey);

        // Act
        ITestService scopedService1, scopedService2;
        using (var scope = container.CreateScope())
        {
            scopedService1 = scope.ServiceFactory.GetKeyedService<ITestService>(serviceKey);
            scopedService2 = scope.ServiceFactory.GetKeyedService<ITestService>(serviceKey);
        }

        // Assert
        Assert.Same(scopedService1, scopedService2); // Same instance within scope
        Assert.True(scopedService1.IsDisposed); // Disposed when scope ends
    }

    [Fact]
    public void KeyedServices_WithComplexDependencyInjection_ShouldInjectCorrectKeyedServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<IKeyedService, KeyedServiceA>("keyA");
        container.RegisterTransient<IKeyedService, KeyedServiceB>("keyB");

        // Act
        var serviceA = container.GetKeyedService<IKeyedService>("keyA");
        var serviceB = container.GetKeyedService<IKeyedService>("keyB");

        // Assert
        Assert.IsType<KeyedServiceA>(serviceA);
        Assert.IsType<KeyedServiceB>(serviceB);
        Assert.Equal("KeyA", serviceA.ServiceKey);
        Assert.Equal("KeyB", serviceB.ServiceKey);
    }

    [Fact]
    public void IsServiceRegistered_WithKeyedService_ShouldCheckCorrectKey()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string registeredKey = "registeredKey";
        const string unregisteredKey = "unregisteredKey";

        container.RegisterSingleton<ITestService, TestService>(registeredKey);

        // Act & Assert
        Assert.True(container.IsServiceRegistered<ITestService>(registeredKey));
        Assert.False(container.IsServiceRegistered<ITestService>(unregisteredKey));
        Assert.False(container.IsServiceRegistered<ITestService>(null)); // Default key
    }

    [Fact]
    public void KeyedServices_WithNullKey_ShouldBehaveLikeDefaultRegistration()
    {
        // Arrange
        using var container = new ServiceContainer();
        var service = new TestService { Name = "NullKeyService" };

        // Act
        container.RegisterSingleton<ITestService>(service, null);
        var resolvedByDefault = container.GetService<ITestService>();
        var resolvedByNullKey = container.GetKeyedService<ITestService>(null);

        // Assert
        Assert.Same(service, resolvedByDefault);
        Assert.Same(service, resolvedByNullKey);
        Assert.Same(resolvedByDefault, resolvedByNullKey);
    }

    [Fact]
    public void UnregisterKeyedService_ShouldRemoveOnlySpecificKeyedService()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string key1 = "key1";
        const string key2 = "key2";

        var service1 = new TestService { Name = "Service1" };
        var service2 = new TestService { Name = "Service2" };

        container.RegisterSingleton<ITestService>(service1, key1);
        container.RegisterSingleton<ITestService>(service2, key2);

        // Act
        container.UnregisterService<ITestService>(key1, out bool successful);

        // Assert
        Assert.True(successful);
        Assert.False(container.IsServiceRegistered<ITestService>(key1));
        Assert.True(container.IsServiceRegistered<ITestService>(key2));
        Assert.True(service1.IsDisposed);
        Assert.False(service2.IsDisposed);
    }
}