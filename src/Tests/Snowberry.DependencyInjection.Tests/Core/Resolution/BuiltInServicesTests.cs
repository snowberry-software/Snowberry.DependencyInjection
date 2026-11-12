using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Tests for built-in services like IServiceProvider, IScope, IServiceScopeFactory, and IServiceFactory.
/// </summary>
public class BuiltInServicesTests
{
    [Theory]
    [InlineData(typeof(IServiceProvider))]
    [InlineData(typeof(IScope))]
    [InlineData(typeof(IServiceScopeFactory))]
    [InlineData(typeof(IServiceFactory))]
    public void GetService_BuiltInService_ShouldResolveWithoutRegistrationAndConsistently(Type serviceType)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act - every way to ask for the default (unkeyed) service must yield the same instance
        object? service = container.GetService(serviceType);
        object? serviceWithNullKey = container.GetKeyedService(serviceType, null);
        object requiredService = container.GetRequiredService(serviceType);

        // Assert
        Assert.IsAssignableFrom(serviceType, service);
        Assert.Same(service, serviceWithNullKey);
        Assert.Same(service, requiredService);
        Assert.Same(service, container.GetService(serviceType));
    }

    [Theory]
    [InlineData(typeof(IServiceProvider))]
    [InlineData(typeof(IScope))]
    [InlineData(typeof(IServiceScopeFactory))]
    [InlineData(typeof(IServiceFactory))]
    public void GetKeyedService_BuiltInService_WithKey_ShouldReturnNull(Type serviceType)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        object? service = container.GetKeyedService(serviceType, "someKey");

        // Assert
        Assert.Null(service);
    }

    [Theory]
    [InlineData(typeof(IServiceProvider))]
    [InlineData(typeof(IScope))]
    [InlineData(typeof(IServiceScopeFactory))]
    [InlineData(typeof(IServiceFactory))]
    public void GetRequiredKeyedService_BuiltInService_WithKey_ShouldThrowNotRegistered(Type serviceType)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.ThrowsAny<ServiceTypeNotRegistered>(() => container.GetRequiredKeyedService(serviceType, "key"));
    }

    [Fact]
    public void GetService_IServiceFactory_ShouldReturnContainerServiceFactory()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var factory = container.GetService<IServiceFactory>();

        // Assert
        Assert.Same(container.ServiceFactory, factory);
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
        Assert.True(scope.IsGlobalScope);
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
        Assert.False(retrievedScope.IsGlobalScope);
        Assert.Same(createdScope, retrievedScope);
    }

    [Theory]
    [InlineData(typeof(IServiceScopeFactory))]
    [InlineData(typeof(IServiceFactory))]
    public void GetService_BuiltInFactory_InScope_ReturnsSameFactoryAsRoot(Type factoryType)
    {
        // Arrange
        using var container = new ServiceContainer();
        var rootFactory = container.GetRequiredService(factoryType);

        using var scope = container.CreateScope();

        // Act
        var scopeFactory = scope.ServiceProvider.GetRequiredService(factoryType);

        // Assert
        Assert.Same(rootFactory, scopeFactory);
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
        Assert.Same(container.ServiceFactory, service.ServiceFactory);
        Assert.Same(container.GetRequiredService<IServiceScopeFactory>(), service.ScopeFactory);
        Assert.Same(container.GetRequiredService<IServiceProvider>(), service.ServiceProvider);

        // Verify the injected scope is the global scope
        Assert.Same(container.GetRequiredService<IScope>(), service.Scope);
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
        Assert.NotSame(scope1, scope2);
    }

    [Fact]
    public void IServiceProvider_FromScope_CanResolveServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ParameterlessService>();

        using var scope = container.CreateScope();
        var serviceProvider = scope.ServiceProvider.GetRequiredService<IServiceProvider>();

        // Act
        object? service = serviceProvider.GetService(typeof(ParameterlessService));

        // Assert
        Assert.IsType<ParameterlessService>(service);
    }

    [Fact]
    public void BuiltInServices_AreNotInServiceDescriptors()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ParameterlessService>();

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

    /// <summary>
    /// A user service that receives every built-in service through its constructor.
    /// </summary>
    private class ServiceWithBuiltInDependencies
    {
        /// <summary>Gets the injected service provider.</summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>Gets the injected scope.</summary>
        public IScope Scope { get; }

        /// <summary>Gets the injected scope factory.</summary>
        public IServiceScopeFactory ScopeFactory { get; }

        /// <summary>Gets the injected service factory.</summary>
        public IServiceFactory ServiceFactory { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceWithBuiltInDependencies"/> class.
        /// </summary>
        /// <param name="serviceProvider">The injected service provider.</param>
        /// <param name="scope">The injected scope.</param>
        /// <param name="scopeFactory">The injected scope factory.</param>
        /// <param name="serviceFactory">The injected service factory.</param>
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
}
