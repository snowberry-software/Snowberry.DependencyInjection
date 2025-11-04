using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Comprehensive tests for all service lifetimes (Singleton, Transient, Scoped) 
/// with iteration patterns to verify correct behavior across multiple scope creations.
/// Tests verify that services receive the correct IServiceProvider and IScope instances.
/// </summary>
public class ServiceLifetimeIterationTests
{
    [Fact]
    public void SingletonLifetime_MultipleScopes_ShouldReturnSameInstanceAndReceiveRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        ServiceWithScopeAndProviderDependencies? firstServiceInstance = null;
        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();

        // Act - Create 3 scopes and resolve services in each
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Also resolve a child service using the constructor-injected ServiceProvider
            var childTestService = service.ServiceProvider.GetRequiredService<ITestService>();

            // Store first instance for comparison
            firstServiceInstance ??= service;

            collectedServices.Add(service);
            collectedTestServices.Add(testService);

            // Assert within iteration
            Assert.NotNull(service);
            Assert.NotNull(service.ServiceProvider);
            Assert.NotNull(service.Scope);

            // Singleton should always be the same instance
            Assert.Same(firstServiceInstance, service);

            // Singleton should always receive the ROOT SCOPE, even when resolved from child scope
            Assert.True(service.Scope.IsGlobalScope);

            // The ITestService should also be the same singleton instance
            Assert.Same(collectedTestServices[0], testService);

            // Child service resolved via constructor-injected ServiceProvider should be the same
            Assert.Same(testService, childTestService);

            // Verify services are not disposed while scopes are active
            Assert.False(service.IsDisposed);
            Assert.False(testService.IsDisposed);
        }

        // Assert - All services should be the same singleton instance
        Assert.Equal(3, collectedServices.Count);
        Assert.Equal(3, collectedTestServices.Count);

        for (int i = 0; i < collectedServices.Count; i++)
        {
            Assert.Same(firstServiceInstance, collectedServices[i]);
            Assert.Same(collectedTestServices[0], collectedTestServices[i]);
            // All singleton instances should have the global scope
            Assert.True(collectedServices[i].Scope.IsGlobalScope);
        }

        // All services should still be alive after child scopes are disposed
        Assert.All(collectedServices, s => Assert.False(s.IsDisposed));
        Assert.All(collectedTestServices, s => Assert.False(s.IsDisposed));

        // Dispose container
        container.Dispose();

        // Now all singleton services should be disposed
        Assert.All(collectedServices, s => Assert.True(s.IsDisposed));
        Assert.All(collectedTestServices, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void TransientLifetime_MultipleScopes_ShouldReturnDifferentInstancesAndReceiveCorrectScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        container.RegisterTransient<ServiceWithScopeAndProviderDependencies>();

        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();
        var childResolvedServices = new List<ITestService>();

        // Act - Create 3 scopes and resolve services in each
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Also resolve a child service using the constructor-injected ServiceProvider
            var childTestService = service.ServiceProvider.GetRequiredService<ITestService>();

            collectedServices.Add(service);
            collectedTestServices.Add(testService);
            childResolvedServices.Add(childTestService);

            // Assert within iteration
            Assert.NotNull(service);
            Assert.NotNull(service.ServiceProvider);
            Assert.NotNull(service.Scope);

            // For transient services, each resolution should receive the current scope
            Assert.Same(scope.ServiceProvider, service.ServiceProvider);
            Assert.Same(scope, service.Scope);

            // Child scopes should not be global
            Assert.False(service.Scope.IsGlobalScope);

            // Child resolved via ServiceProvider should be different (transient)
            Assert.NotSame(testService, childTestService);

            // But both should be from the same scope
            Assert.Same(scope.ServiceProvider, service.ServiceProvider);

            // Verify services are not disposed while scope is active
            Assert.False(service.IsDisposed);
            Assert.False(testService.IsDisposed);
            Assert.False(childTestService.IsDisposed);
        }

        // Assert - All services should be different transient instances
        Assert.Equal(3, collectedServices.Count);
        Assert.Equal(3, collectedTestServices.Count);
        Assert.Equal(3, childResolvedServices.Count);

        // Verify all instances are unique
        for (int i = 0; i < collectedServices.Count; i++)
        {
            for (int j = i + 1; j < collectedServices.Count; j++)
            {
                Assert.NotSame(collectedServices[i], collectedServices[j]);
                Assert.NotSame(collectedTestServices[i], collectedTestServices[j]);
                Assert.NotSame(childResolvedServices[i], childResolvedServices[j]);
            }
        }

        // All transient services should be disposed after their scopes are disposed
        Assert.All(collectedServices, s => Assert.True(s.IsDisposed));
        Assert.All(collectedTestServices, s => Assert.True(s.IsDisposed));
        Assert.All(childResolvedServices, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void ScopedLifetime_MultipleScopes_ShouldReturnDifferentInstancesPerScopeAndReceiveCorrectScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<ServiceWithScopeAndProviderDependencies>();

        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();

        // Act - Create 3 scopes and resolve services in each
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();

            // Resolve twice in the same scope to verify scoped behavior
            var service1 = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var service2 = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            var testService1 = scope.ServiceProvider.GetRequiredService<ITestService>();
            var testService2 = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Resolve using constructor-injected ServiceProvider
            var childTestService = service1.ServiceProvider.GetRequiredService<ITestService>();

            collectedServices.Add(service1);
            collectedTestServices.Add(testService1);

            // Assert within iteration
            Assert.NotNull(service1);
            Assert.NotNull(service1.ServiceProvider);
            Assert.NotNull(service1.Scope);

            // Scoped services should return the same instance within the same scope
            Assert.Same(service1, service2);
            Assert.Same(testService1, testService2);

            // Child resolved via constructor-injected ServiceProvider should also be same (scoped)
            Assert.Same(testService1, childTestService);

            // Each scoped service should receive the current scope
            Assert.Same(scope.ServiceProvider, service1.ServiceProvider);
            Assert.Same(scope, service1.Scope);

            // Child scopes should not be global
            Assert.False(service1.Scope.IsGlobalScope);

            // Verify services are not disposed while scope is active
            Assert.False(service1.IsDisposed);
            Assert.False(testService1.IsDisposed);
        }

        // Assert - Each scope should have created different instances
        Assert.Equal(3, collectedServices.Count);
        Assert.Equal(3, collectedTestServices.Count);

        // Verify all instances are unique across different scopes
        for (int i = 0; i < collectedServices.Count; i++)
        {
            for (int j = i + 1; j < collectedServices.Count; j++)
            {
                Assert.NotSame(collectedServices[i], collectedServices[j]);
                Assert.NotSame(collectedTestServices[i], collectedTestServices[j]);
            }
        }

        // All scoped services should be disposed after their scopes are disposed
        Assert.All(collectedServices, s => Assert.True(s.IsDisposed));
        Assert.All(collectedTestServices, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void SingletonLifetime_WithRootScopeResolution_ShouldReceiveRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        // Act - Resolve singleton from root scope
        var serviceFromRoot = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // Assert
        Assert.NotNull(serviceFromRoot);
        Assert.NotNull(serviceFromRoot.Scope);
        Assert.True(serviceFromRoot.Scope.IsGlobalScope);

        // Act - Resolve same singleton from child scopes
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var serviceFromChild = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Assert - Should be the same singleton instance
            Assert.Same(serviceFromRoot, serviceFromChild);

            // The singleton ALWAYS has the root scope, regardless of where it's resolved from
            Assert.True(serviceFromChild.Scope.IsGlobalScope);
        }

        Assert.False(serviceFromRoot.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(serviceFromRoot.IsDisposed);
    }

    [Fact]
    public void MixedLifetimes_MultipleScopes_ShouldHandleEachLifetimeCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        ITestService? singletonInstance = null;
        var transientInstances = new List<IDependentService>();
        var scopedInstances = new List<IComplexService>();

        // Act - Create 3 scopes and resolve services in each
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();

            var singleton = scope.ServiceProvider.GetRequiredService<ITestService>();
            var transient = scope.ServiceProvider.GetRequiredService<IDependentService>();
            var scoped = scope.ServiceProvider.GetRequiredService<IComplexService>();

            singletonInstance ??= singleton;
            transientInstances.Add(transient);
            scopedInstances.Add(scoped);

            // Assert within iteration
            // Singleton should always be the same
            Assert.Same(singletonInstance, singleton);

            // Transient should be different each time
            Assert.All(transientInstances.Take(i), t => Assert.NotSame(t, transient));

            // Scoped should be the same within this scope
            var scoped2 = scope.ServiceProvider.GetRequiredService<IComplexService>();
            Assert.Same(scoped, scoped2);

            // Verify that the singleton dependency in scoped/transient is the same singleton
            Assert.Same(singletonInstance, transient.PrimaryDependency);
            Assert.Same(singletonInstance, scoped.TestService);
        }

        // Assert - Verify instance counts and uniqueness
        Assert.Equal(3, transientInstances.Count);
        Assert.Equal(3, scopedInstances.Count);

        // All transient instances should be unique
        for (int i = 0; i < transientInstances.Count; i++)
        {
            for (int j = i + 1; j < transientInstances.Count; j++)
            {
                Assert.NotSame(transientInstances[i], transientInstances[j]);
            }
        }

        // All scoped instances should be unique
        for (int i = 0; i < scopedInstances.Count; i++)
        {
            for (int j = i + 1; j < scopedInstances.Count; j++)
            {
                Assert.NotSame(scopedInstances[i], scopedInstances[j]);
            }
        }

        // After scopes are disposed
        Assert.False(singletonInstance!.IsDisposed); // Singleton still alive
        Assert.All(transientInstances, t => Assert.True(t.IsDisposed)); // Transients disposed
        Assert.All(scopedInstances, s => Assert.True(s.IsDisposed)); // Scoped disposed

        // Dispose container
        container.Dispose();
        Assert.True(singletonInstance.IsDisposed);
    }

    [Fact]
    public void ScopedLifetime_ResolvedFromRootScope_ShouldBeGlobalScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithScopeAndProviderDependencies>();

        // Act - Resolve scoped service from root scope (which is treated as a scope)
        var serviceFromRoot = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // Assert - Root scope IS the global scope
        Assert.NotNull(serviceFromRoot);
        Assert.True(serviceFromRoot.Scope.IsGlobalScope);

        // Act - Resolve from child scopes
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var serviceFromChild = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Assert - Should be different instances and child scope should not be global
            Assert.NotSame(serviceFromRoot, serviceFromChild);
            Assert.False(serviceFromChild.Scope.IsGlobalScope);
        }
    }

    [Fact]
    public void TransientLifetime_ResolvedMultipleTimesInSameScope_ShouldCreateNewInstancesWithSameScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ServiceWithScopeAndProviderDependencies>();

        // Act & Assert - Create 3 scopes and resolve multiple times in each
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var instances = new List<ServiceWithScopeAndProviderDependencies>();

            // Resolve 3 times within the same scope
            for (int j = 0; j < 3; j++)
            {
                var instance = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
                instances.Add(instance);

                // Each instance should receive the same scope
                Assert.Same(scope.ServiceProvider, instance.ServiceProvider);
                Assert.Same(scope, instance.Scope);
                Assert.False(instance.Scope.IsGlobalScope);
            }

            // All instances within the same scope should be different (transient)
            Assert.Equal(3, instances.Count);
            Assert.NotSame(instances[0], instances[1]);
            Assert.NotSame(instances[1], instances[2]);
            Assert.NotSame(instances[0], instances[2]);
        }
    }

    [Fact]
    public void SingletonLifetime_FirstCreatedInChildScope_ShouldAlwaysReceiveRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        ServiceWithScopeAndProviderDependencies? singletonInstance = null;

        // Act - First create singleton in a child scope
        using (var scope1 = container.CreateScope())
        {
            singletonInstance = scope1.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Even though resolved from a child scope, singleton should receive the ROOT SCOPE
            Assert.True(singletonInstance.Scope.IsGlobalScope);
        }

        // The singleton is still alive after the scope is disposed
        Assert.False(singletonInstance.IsDisposed);

        // Act - Resolve the same singleton from other scopes
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();
            var resolvedService = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Resolve using the constructor-injected ServiceProvider
            var childResolvedService = resolvedService.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Should be the exact same singleton instance
            Assert.Same(singletonInstance, resolvedService);
            Assert.Same(singletonInstance, childResolvedService);

            // Should ALWAYS reference the root scope
            Assert.True(resolvedService.Scope.IsGlobalScope);
            Assert.True(childResolvedService.Scope.IsGlobalScope);
        }

        // Singleton should still be alive
        Assert.False(singletonInstance.IsDisposed);

        // Dispose container - now singleton should be disposed
        container.Dispose();
        Assert.True(singletonInstance.IsDisposed);
    }

    [Fact]
    public void SingletonLifetime_ResolvedViaConstructorInjectedServiceProvider_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        // Act - Create multiple scopes and resolve services
        var directlyResolvedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var childResolvedTestServices = new List<ITestService>();

        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();

            // Resolve the service with injected ServiceProvider
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            directlyResolvedServices.Add(service);

            // Use the constructor-injected ServiceProvider to resolve ITestService
            var testServiceViaInjected = service.ServiceProvider.GetRequiredService<ITestService>();
            childResolvedTestServices.Add(testServiceViaInjected);

            // Also resolve directly from the scope
            var testServiceDirect = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Both should be the same singleton instance
            Assert.Same(testServiceViaInjected, testServiceDirect);
        }

        // Assert - All resolved instances should be the same singleton
        Assert.Equal(3, directlyResolvedServices.Count);
        Assert.Equal(3, childResolvedTestServices.Count);

        for (int i = 1; i < directlyResolvedServices.Count; i++)
        {
            Assert.Same(directlyResolvedServices[0], directlyResolvedServices[i]);
            Assert.Same(childResolvedTestServices[0], childResolvedTestServices[i]);
        }

        // All singletons should have root scope
        Assert.All(directlyResolvedServices, s => Assert.True(s.Scope.IsGlobalScope));
    }

    [Fact]
    public void ScopedLifetime_ResolvedViaConstructorInjectedServiceProvider_ShouldReturnSameScopedInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<ServiceWithScopeAndProviderDependencies>();

        // Act - Create multiple scopes
        for (int i = 0; i < 3; i++)
        {
            using var scope = container.CreateScope();

            // Resolve the service with injected ServiceProvider
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            // Use the constructor-injected ServiceProvider to resolve ITestService
            var testServiceViaInjected = service.ServiceProvider.GetRequiredService<ITestService>();

            // Also resolve directly from the scope
            var testServiceDirect = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Within the same scope, both should be the same instance
            Assert.Same(testServiceViaInjected, testServiceDirect);

            // The service's injected ServiceProvider should be the same as the scope's ServiceProvider
            Assert.Same(scope.ServiceProvider, service.ServiceProvider);
            Assert.Same(scope, service.Scope);
        }
    }
}
