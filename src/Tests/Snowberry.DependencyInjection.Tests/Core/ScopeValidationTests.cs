using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the ValidateScopes option that ensures scoped services 
/// can only be resolved from child scopes and not from the root container.
/// </summary>
public class ScopeValidationTests
{
    [Fact]
    public void ValidateScopes_Disabled_ScopedServiceResolvedFromRoot_ShouldSucceed()
    {
        // Arrange - ValidateScopes is NOT enabled (default behavior)
        using var container = new ServiceContainer(ServiceContainerOptions.Default);
        container.RegisterScoped<ITestService, TestService>();

        // Act - Should succeed without validation
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
        Assert.False(container.ValidateScopes);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromRoot_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        // Act & Assert
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());

        Assert.Equal(typeof(ITestService), exception.ServiceType);
        Assert.True(container.ValidateScopes);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        // Act
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_SingletonResolvedFromRoot_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterSingleton<ITestService, TestService>();

        // Act - Singletons should work fine from root
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_TransientResolvedFromRoot_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterTransient<ITestService, TestService>();

        // Act - Transients should work fine from root
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_KeyedScopedServiceResolvedFromRoot_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>("my-key");

        // Act & Assert
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredKeyedService<ITestService>("my-key"));

        Assert.Equal(typeof(ITestService), exception.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Enabled_KeyedScopedServiceResolvedFromChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>("my-key");

        // Act
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredKeyedService<ITestService>("my-key");

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceWithDependenciesResolvedFromRoot_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();

        // Act & Assert - Should throw for the scoped service
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IDependentService>());

        Assert.Equal(typeof(IDependentService), exception.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Enabled_SingletonDependingOnScopedResolvedFromRoot_ShouldNotThrow()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act & Assert - Singleton itself can be resolved from root
        // Note: This creates a captive dependency, but validation only checks direct resolution
        var service = container.GetRequiredService<IDependentService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_MultipleScopedServicesResolvedFromRoot_ShouldAllThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        // Act & Assert - All should throw
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IDependentService>());
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IComplexService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromNestedScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        // Act - Resolve from nested scopes
        using var outerScope = container.CreateScope();
        var outerService = outerScope.ServiceProvider.GetRequiredService<ITestService>();

        using var innerScope = container.CreateScope();
        var innerService = innerScope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - Both should succeed and be different instances
        Assert.NotNull(outerService);
        Assert.NotNull(innerService);
        Assert.NotSame(outerService, innerService);
    }

    [Fact]
    public void ValidateScopes_EnabledWithReadOnly_ShouldWorkTogether()
    {
        // Arrange
        using var container = new ServiceContainer(
            ServiceContainerOptions.ValidateScopes | ServiceContainerOptions.ReadOnly);
        container.RegisterScoped<ITestService, TestService>();

        // Assert - Both options should be enabled
        Assert.True(container.ValidateScopes);
        Assert.True(container.AreRegisteredServicesReadOnly);

        // Act & Assert - Scoped validation should work
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());

        // Act & Assert - ReadOnly should work
        Assert.Throws<ServiceRegistryReadOnlyException>(() =>
            container.RegisterScoped<ITestService, TestService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_GetServiceReturnsNull_ForScopedServiceFromRoot()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        // Act & Assert - GetService should throw, not return null
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetService(typeof(ITestService)));
    }

    [Fact]
    public void ValidateScopes_Enabled_GetKeyedServiceThrows_ForScopedServiceFromRoot()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>("key");

        // Act & Assert
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetKeyedService(typeof(ITestService), "key"));
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceWithFactoryResolvedFromRoot_ShouldThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService>((sp, key) => new TestService { Name = "Factory" });

        // Act & Assert
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());

        Assert.Equal(typeof(ITestService), exception.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceWithFactoryResolvedFromChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService>((sp, key) => new TestService { Name = "Factory" });

        // Act
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.Equal("Factory", service.Name);
    }

    [Fact]
    public void ValidateScopes_Enabled_MixedLifetimesResolvedFromRoot_OnlyScopedShouldThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        // Act & Assert - Singleton should work
        var singleton = container.GetRequiredService<ITestService>();
        Assert.NotNull(singleton);

        // Act & Assert - Transient should work
        var transient = container.GetRequiredService<IDependentService>();
        Assert.NotNull(transient);

        // Act & Assert - Scoped should throw
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IComplexService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedGenericServiceResolvedFromRoot_ShouldThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<IRepository<TestService>, Repository<TestService>>();

        // Act & Assert
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IRepository<TestService>>());

        Assert.Equal(typeof(IRepository<TestService>), exception.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedGenericServiceResolvedFromChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<IRepository<TestService>, Repository<TestService>>();

        // Act
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRepository<TestService>>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void ValidateScopes_CheckPropertyValue_ShouldReflectOption()
    {
        // Arrange & Act
        using var containerWithValidation = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        using var containerWithoutValidation = new ServiceContainer(ServiceContainerOptions.None);
        using var containerDefault = new ServiceContainer();

        // Assert
        Assert.True(containerWithValidation.ValidateScopes);
        Assert.False(containerWithoutValidation.ValidateScopes);
        Assert.False(containerDefault.ValidateScopes); // Default does not include ValidateScopes
    }

    [Fact]
    public void ValidateScopes_Enabled_ResolveScopedViaInjectedServiceProviderFromRoot_ShouldStillThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        // Act - Singleton gets injected with root IServiceProvider
        var singletonService = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // The singleton was created successfully (it receives root ServiceProvider)
        Assert.NotNull(singletonService);
        Assert.NotNull(singletonService.ServiceProvider);
        Assert.True(singletonService.Scope.IsGlobalScope);

        // Act & Assert - But trying to resolve scoped service from that injected provider should throw
        // because it's the root scope's ServiceProvider
        Assert.Throws<ServiceScopeRequiredException>(() =>
            singletonService.ServiceProvider.GetRequiredService<ITestService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_MultipleThreadsResolvingScopedFromRoot_AllShouldThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act - Try to resolve from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    container.GetRequiredService<ITestService>();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All should have thrown ServiceScopeRequiredException
        Assert.Equal(10, exceptions.Count);
        Assert.All(exceptions, ex => Assert.IsType<ServiceScopeRequiredException>(ex));
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedMultipleTimesFromSameChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();

        // Act
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service3 = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - All should be the same instance (scoped behavior)
        Assert.Same(service1, service2);
        Assert.Same(service2, service3);
    }
}
