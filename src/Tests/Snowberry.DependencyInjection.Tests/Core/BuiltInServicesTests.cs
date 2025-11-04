using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for built-in services like IServiceProvider, IScope, IServiceScopeFactory, and IServiceFactory.
/// </summary>
public class BuiltInServicesTests
{
    [Fact]
    public void GetService_IServiceProvider_ReturnsServiceProvider()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var serviceProvider = container.GetService<IServiceProvider>();

        // Assert
        Assert.NotNull(serviceProvider);
        Assert.IsAssignableFrom<IServiceProvider>(serviceProvider);
    }

    [Fact]
    public void GetService_IServiceProvider_InScope_ReturnsScopeProvider()
    {
        // Arrange
        using var container = new ServiceContainer();
        using var scope = container.CreateScope();

        // Act
        var serviceProvider = scope.ServiceProvider.GetService<IServiceProvider>();

        // Assert
        Assert.NotNull(serviceProvider);
        Assert.Same(scope.ServiceProvider, serviceProvider);

        // Verify it's different from root
        var rootServiceProvider = container.GetService<IServiceProvider>();
        Assert.NotSame(rootServiceProvider, serviceProvider);
    }

    [Fact]
    public void GetService_IScope_ReturnsRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var scope = container.GetService<IScope>();

        // Assert
        Assert.NotNull(scope);
        Assert.True(scope!.IsGlobalScope);
    }

    [Fact]
    public void GetService_IScope_InScope_ReturnsScopedScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        using var createdScope = container.CreateScope();

        // Act
        var retrievedScope = createdScope.ServiceProvider.GetService<IScope>();

        // Assert
        Assert.NotNull(retrievedScope);
        Assert.False(retrievedScope!.IsGlobalScope);
        Assert.Same(createdScope, retrievedScope);
    }

    [Fact]
    public void GetService_IServiceScopeFactory_ReturnsFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetService<IServiceScopeFactory>();

        // Assert
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IServiceScopeFactory>(factory);
    }

    [Fact]
    public void GetService_IServiceScopeFactory_InScope_ReturnsSameFactory()
    {
        // Arrange
        using var container = new ServiceContainer();
        var rootFactory = container.GetRequiredService<IServiceScopeFactory>();

        using var scope = container.CreateScope();

        // Act
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

        // Assert
        Assert.NotNull(scopeFactory);
        Assert.Same(rootFactory, scopeFactory);
    }

    [Fact]
    public void GetService_IServiceFactory_ReturnsFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetService<IServiceFactory>();

        // Assert
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IServiceFactory>(factory);
    }

    [Fact]
    public void GetService_IServiceFactory_InScope_ReturnsSameFactory()
    {
        // Arrange
        using var container = new ServiceContainer();
        var rootFactory = container.ServiceFactory;

        using var scope = container.CreateScope();

        // Act
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceFactory>();

        // Assert
        Assert.NotNull(scopeFactory);
        Assert.Same(rootFactory, scopeFactory);
    }

    [Fact]
    public void GetKeyedService_IServiceProvider_WithNullKey_ReturnsServiceProvider()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var serviceProvider = container.GetKeyedService<IServiceProvider>(null);

        // Assert
        Assert.NotNull(serviceProvider);
        Assert.IsAssignableFrom<IServiceProvider>(serviceProvider);
    }

    [Fact]
    public void GetKeyedService_IServiceProvider_WithKey_ReturnsNullServiceProvider()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var serviceProvider = container.GetKeyedService<IServiceProvider>("someKey");

        // Assert
        Assert.Null(serviceProvider);
    }

    [Fact]
    public void GetKeyedService_IScope_WithNullKey_ReturnsRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var scope = container.GetKeyedService<IScope>(null);

        // Assert
        Assert.NotNull(scope);
        Assert.True(scope!.IsGlobalScope);
    }

    [Fact]
    public void GetKeyedService_IScope_WithKey_ReturnsNullScope()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var scope = container.GetKeyedService<IScope>("someKey");

        // Assert
        Assert.Null(scope);
    }

    [Fact]
    public void GetKeyedService_IServiceScopeFactory_WithNullKey_ReturnsFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetKeyedService<IServiceScopeFactory>(null);

        // Assert
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IServiceScopeFactory>(factory);
    }

    [Fact]
    public void GetKeyedService_IServiceScopeFactory_WithKey_ReturnsNullFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetKeyedService<IServiceScopeFactory>("someKey");

        // Assert
        Assert.Null(factory);
    }

    [Fact]
    public void GetKeyedService_IServiceFactory_WithNullKey_ReturnsFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetKeyedService<IServiceFactory>(null);

        // Assert
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IServiceFactory>(factory);
    }

    [Fact]
    public void GetKeyedService_IServiceFactory_WithKey_ReturnsNullFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetKeyedService<IServiceFactory>("someKey");

        // Assert
        Assert.Null(factory);
    }

    [Fact]
    public void BuiltInServices_CanBeInjectedIntoUserServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithBuiltInDependencies>();

        // Act
        var service = container.GetRequiredService<ServiceWithBuiltInDependencies>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.ServiceProvider);
        Assert.NotNull(service.Scope);
        Assert.NotNull(service.ScopeFactory);
        Assert.NotNull(service.ServiceFactory);

        // Verify the injected scope is the global scope
        Assert.True(service.Scope.IsGlobalScope);
    }

    [Fact]
    public void BuiltInServices_InScope_ReflectScopeContext()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithBuiltInDependencies>();

        using var scope = container.CreateScope();

        // Act
        var service = scope.ServiceProvider.GetRequiredService<ServiceWithBuiltInDependencies>();

        // Assert
        Assert.NotNull(service);
        Assert.Same(scope.ServiceProvider, service.ServiceProvider);
        Assert.Same(scope, service.Scope);
        Assert.False(service.Scope.IsGlobalScope);
    }

    [Fact]
    public void BuiltInServices_MultipleScopesHaveDifferentProviders()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithBuiltInDependencies>();

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.ServiceProvider.GetRequiredService<ServiceWithBuiltInDependencies>();
        var service2 = scope2.ServiceProvider.GetRequiredService<ServiceWithBuiltInDependencies>();

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
        Assert.NotSame(service1.ServiceProvider, service2.ServiceProvider);
        Assert.NotSame(service1.Scope, service2.Scope);
        Assert.Same(service1.ServiceFactory, service2.ServiceFactory);
    }

    [Fact]
    public void IServiceScopeFactory_CanCreateMultipleScopes()
    {
        // Arrange
        using var container = new ServiceContainer();
        var factory = container.GetRequiredService<IServiceScopeFactory>();

        // Act
        using var scope1 = factory.CreateScope();
        using var scope2 = factory.CreateScope();

        // Assert
        Assert.NotNull(scope1);
        Assert.NotNull(scope2);
        Assert.NotSame(scope1, scope2);
    }

    [Fact]
    public void IServiceProvider_FromScope_CanResolveServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleTestService>();

        using var scope = container.CreateScope();
        var serviceProvider = scope.ServiceProvider.GetRequiredService<IServiceProvider>();

        // Act
        object? service = serviceProvider.GetService(typeof(SimpleTestService));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<SimpleTestService>(service);
    }

    [Fact]
    public void BuiltInServices_DoNotRequireRegistration()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert - Should not throw
        Assert.NotNull(container.GetService<IServiceProvider>());
        Assert.NotNull(container.GetService<IScope>());
        Assert.NotNull(container.GetService<IServiceScopeFactory>());
        Assert.NotNull(container.GetService<IServiceFactory>());
    }

    [Fact]
    public void BuiltInServices_AreNotInServiceDescriptors()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleTestService>();

        // Act
        var descriptors = container.GetServiceDescriptors();

        // Assert
        Assert.Single(descriptors);
        Assert.All(descriptors, d =>
        {
            Assert.NotEqual(typeof(IServiceProvider), d.ServiceType);
            Assert.NotEqual(typeof(IScope), d.ServiceType);
            Assert.NotEqual(typeof(IServiceScopeFactory), d.ServiceType);
            Assert.NotEqual(typeof(IServiceFactory), d.ServiceType);
        });
    }

    [Fact]
    public void GetRequiredService_BuiltInServices_ReturnsNonNullValues()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        var serviceProvider = container.GetRequiredService<IServiceProvider>();
        var scope = container.GetRequiredService<IScope>();
        var scopeFactory = container.GetRequiredService<IServiceScopeFactory>();
        var serviceFactory = container.GetRequiredService<IServiceFactory>();

        Assert.NotNull(serviceProvider);
        Assert.NotNull(scope);
        Assert.NotNull(scopeFactory);
        Assert.NotNull(serviceFactory);
    }

    [Fact]
    public void GetRequiredKeyedService_BuiltInServices_ThrowsNotRegistered()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.ThrowsAny<ServiceTypeNotRegistered>(() =>
        {
            var serviceProvider = container.GetRequiredKeyedService<IServiceProvider>("key");
        });

        Assert.ThrowsAny<ServiceTypeNotRegistered>(() =>
        {
            var scope = container.GetRequiredKeyedService<IScope>("key");
        });

        Assert.ThrowsAny<ServiceTypeNotRegistered>(() =>
        {
            var scopeFactory = container.GetRequiredKeyedService<IServiceScopeFactory>("key");
        });

        Assert.ThrowsAny<ServiceTypeNotRegistered>(() =>
        {
            var serviceFactory = container.GetRequiredKeyedService<IServiceFactory>("key");
        });
    }

    [Fact]
    public void IServiceProvider_ResolutionConsistency_SameProviderReturnsSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        var provider1 = container.GetService<IServiceProvider>();
        var provider2 = container.GetService<IServiceProvider>();

        // Assert
        Assert.Same(provider1, provider2);
    }

    [Fact]
    public void IScope_ResolutionConsistency_SameProviderReturnsSameScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        var scope1 = container.GetService<IScope>();
        var scope2 = container.GetService<IScope>();

        // Assert
        Assert.Same(scope1, scope2);
    }

    [Fact]
    public void IServiceScopeFactory_ResolutionConsistency_SameProviderReturnsSameFactory()
    {
        // Arrange
        using var container = new ServiceContainer();
        var factory1 = container.GetService<IServiceScopeFactory>();
        var factory2 = container.GetService<IServiceScopeFactory>();

        // Assert
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void IServiceFactory_ResolutionConsistency_SameProviderReturnsSameFactory()
    {
        // Arrange
        using var container = new ServiceContainer();
        var factory1 = container.GetService<IServiceFactory>();
        var factory2 = container.GetService<IServiceFactory>();

        // Assert
        Assert.Same(factory1, factory2);
    }

    // Test helper classes
    private class ServiceWithBuiltInDependencies
    {
        public IServiceProvider ServiceProvider { get; }
        public IScope Scope { get; }
        public IServiceScopeFactory ScopeFactory { get; }
        public IServiceFactory ServiceFactory { get; }

        public ServiceWithBuiltInDependencies(
            IServiceProvider serviceProvider,
            IScope scope,
            IServiceScopeFactory scopeFactory,
            IServiceFactory serviceFactory)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            ServiceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
        }
    }

    private class SimpleTestService
    {
        public string Message => "Test";
    }
}
