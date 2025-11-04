using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests to verify that singleton services always register their disposables 
/// in the root scope, regardless of where they are first resolved.
/// This ensures singletons are disposed when the container is disposed, not when child scopes are disposed.
/// </summary>
public class SingletonScopeDisposalTests
{
    [Fact]
    public void Singleton_FirstResolvedInChildScope_ShouldRegisterDisposableInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        TestService singletonService;

        // Act - First resolve the singleton within a child scope
        using (var childScope = container.CreateScope())
        {
            singletonService = (TestService)childScope.ServiceProvider.GetRequiredService<ITestService>();

            // Assert - Singleton should NOT be registered in the child scope's disposable container
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            
            // Assert - Singleton SHOULD be registered in the root scope's disposable container
            Assert.Equal(1, container.DisposableContainer.DisposableCount);
            
            Assert.False(singletonService.IsDisposed);
        }

        // After child scope disposal, singleton should still not be disposed
        Assert.False(singletonService.IsDisposed);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);

        // Dispose container
        container.Dispose();

        // Now the singleton should be disposed
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public void Singleton_FirstResolvedInRootScope_ShouldRegisterDisposableInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act - First resolve the singleton in the root scope
        var singletonService = (TestService)container.GetRequiredService<ITestService>();

        // Assert
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
        Assert.False(singletonService.IsDisposed);

        // Create a child scope and resolve the same singleton
        using (var childScope = container.CreateScope())
        {
            var sameService = childScope.ServiceProvider.GetRequiredService<ITestService>();
            
            // Should be the exact same instance
            Assert.Same(singletonService, sameService);
            
            // Child scope should not track this singleton
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
        }

        // After child scope disposal, singleton should still not be disposed
        Assert.False(singletonService.IsDisposed);

        // Dispose container
        container.Dispose();

        // Now the singleton should be disposed
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public void MultipleSingletons_FirstResolvedInDifferentChildScopes_ShouldAllRegisterInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<IComplexService, ComplexService>();

        TestService testService;
        DependentService dependentService;
        ComplexService complexService;

        // Act - Resolve different singletons in different child scopes
        using (var scope1 = container.CreateScope())
        {
            testService = (TestService)scope1.ServiceProvider.GetRequiredService<ITestService>();
            
            Assert.Equal(0, scope1.DisposableContainer.DisposableCount);
            Assert.Equal(1, container.DisposableContainer.DisposableCount);
        }

        using (var scope2 = container.CreateScope())
        {
            dependentService = (DependentService)scope2.ServiceProvider.GetRequiredService<IDependentService>();
            
            // DependentService depends on ITestService, so both should be created
            Assert.Equal(0, scope2.DisposableContainer.DisposableCount);
            Assert.Equal(2, container.DisposableContainer.DisposableCount); // TestService + DependentService
        }

        using (var scope3 = container.CreateScope())
        {
            complexService = (ComplexService)scope3.ServiceProvider.GetRequiredService<IComplexService>();
            
            Assert.Equal(0, scope3.DisposableContainer.DisposableCount);
            Assert.Equal(3, container.DisposableContainer.DisposableCount); // All three services
        }

        // After all child scopes are disposed, singletons should still not be disposed
        Assert.False(testService.IsDisposed);
        Assert.False(dependentService.IsDisposed);
        Assert.False(complexService.IsDisposed);

        // Dispose container
        container.Dispose();

        // Now all singletons should be disposed
        Assert.True(testService.IsDisposed);
        Assert.True(dependentService.IsDisposed);
        Assert.True(complexService.IsDisposed);
    }

    [Fact]
    public void Singleton_WithFactory_FirstResolvedInChildScope_ShouldRegisterInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) => new TestService { Name = "FactoryCreated" });

        TestService singletonService;

        // Act - Resolve singleton created by factory in child scope
        using (var childScope = container.CreateScope())
        {
            singletonService = (TestService)childScope.ServiceProvider.GetRequiredService<ITestService>();

            // Assert
            Assert.Equal("FactoryCreated", singletonService.Name);
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(1, container.DisposableContainer.DisposableCount);
        }

        // After child scope disposal
        Assert.False(singletonService.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public void Singleton_ResolvedInNestedScopes_ShouldOnlyRegisterOnceInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        TestService singletonService;

        // Act - Resolve singleton in nested child scopes
        using (var outerScope = container.CreateScope())
        {
            singletonService = (TestService)outerScope.ServiceProvider.GetRequiredService<ITestService>();
            
            Assert.Equal(0, outerScope.DisposableContainer.DisposableCount);
            Assert.Equal(1, container.DisposableContainer.DisposableCount);

            using (var innerScope = container.CreateScope())
            {
                var sameService = innerScope.ServiceProvider.GetRequiredService<ITestService>();
                
                Assert.Same(singletonService, sameService);
                Assert.Equal(0, innerScope.DisposableContainer.DisposableCount);
                Assert.Equal(1, container.DisposableContainer.DisposableCount); // Still only 1
            }

            // After inner scope disposal
            Assert.Equal(0, outerScope.DisposableContainer.DisposableCount);
            Assert.Equal(1, container.DisposableContainer.DisposableCount);
            Assert.False(singletonService.IsDisposed);
        }

        // After outer scope disposal
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
        Assert.False(singletonService.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public void MixedLifetimes_InChildScope_ShouldRegisterDisposablesInCorrectContainers()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();
        container.RegisterTransient<IComplexService, ComplexService>();

        TestService singletonService;
        DependentService scopedService;
        ComplexService transientService;

        // Act - Resolve all services in a child scope
        using (var childScope = container.CreateScope())
        {
            singletonService = (TestService)childScope.ServiceProvider.GetRequiredService<ITestService>();
            scopedService = (DependentService)childScope.ServiceProvider.GetRequiredService<IDependentService>();
            transientService = (ComplexService)childScope.ServiceProvider.GetRequiredService<IComplexService>();

            // Assert - Singleton in root, scoped and transient in child scope
            Assert.Equal(1, container.DisposableContainer.DisposableCount); // Only singleton
            Assert.Equal(2, childScope.DisposableContainer.DisposableCount); // Scoped + Transient

            Assert.False(singletonService.IsDisposed);
            Assert.False(scopedService.IsDisposed);
            Assert.False(transientService.IsDisposed);
        }

        // After child scope disposal
        Assert.False(singletonService.IsDisposed); // Singleton survives
        Assert.True(scopedService.IsDisposed);     // Scoped disposed
        Assert.True(transientService.IsDisposed);  // Transient disposed

        // Dispose container
        container.Dispose();
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public void KeyedSingleton_FirstResolvedInChildScope_ShouldRegisterInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, KeyedServiceA>("KeyA");
        container.RegisterSingleton<IKeyedService, KeyedServiceB>("KeyB");

        KeyedServiceA serviceA;
        KeyedServiceB serviceB;

        // Act - Resolve keyed singletons in child scope
        using (var childScope = container.CreateScope())
        {
            serviceA = (KeyedServiceA)childScope.ServiceProvider.GetRequiredKeyedService<IKeyedService>("KeyA");
            serviceB = (KeyedServiceB)childScope.ServiceProvider.GetRequiredKeyedService<IKeyedService>("KeyB");

            // Assert
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(2, container.DisposableContainer.DisposableCount);
        }

        // After child scope disposal
        Assert.False(serviceA.IsDisposed);
        Assert.False(serviceB.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(serviceA.IsDisposed);
        Assert.True(serviceB.IsDisposed);
    }

    [Fact]
    public void SingletonWithDependencies_FirstResolvedInChildScope_ShouldRegisterAllInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        DependentService dependentService;
        TestService testService;

        // Act - Resolve singleton with dependencies in child scope
        using (var childScope = container.CreateScope())
        {
            dependentService = (DependentService)childScope.ServiceProvider.GetRequiredService<IDependentService>();
            testService = (TestService)dependentService.PrimaryDependency;

            // Assert - Both singleton and its dependency should be in root scope
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(2, container.DisposableContainer.DisposableCount);
            
            Assert.False(testService.IsDisposed);
            Assert.False(dependentService.IsDisposed);
        }

        // After child scope disposal, both should still be alive
        Assert.False(testService.IsDisposed);
        Assert.False(dependentService.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(testService.IsDisposed);
        Assert.True(dependentService.IsDisposed);
    }

    [Fact]
    public void UserProvidedSingletonInstance_ShouldNotBeTrackedInAnyDisposableContainer()
    {
        // Arrange
        var userProvidedService = new TestService { Name = "UserProvided" };
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>(userProvidedService);

        // Act - Resolve in child scope
        using (var childScope = container.CreateScope())
        {
            var resolvedService = childScope.ServiceProvider.GetRequiredService<ITestService>();

            // Assert
            Assert.Same(userProvidedService, resolvedService);
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(0, container.DisposableContainer.DisposableCount); // User instances not tracked
        }

        // After child scope disposal
        Assert.False(userProvidedService.IsDisposed);

        // Dispose container - user provided instance should still not be disposed
        container.Dispose();
        Assert.False(userProvidedService.IsDisposed);
    }

    [Fact]
    public void SingletonCreatedBeforeChildScope_ThenResolvedInChildScope_ShouldNotMoveToChildScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act - Create singleton in root scope first
        var singletonFromRoot = (TestService)container.GetRequiredService<ITestService>();
        
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
        Assert.False(singletonFromRoot.IsDisposed);

        // Now resolve in child scope
        using (var childScope = container.CreateScope())
        {
            var singletonFromChild = childScope.ServiceProvider.GetRequiredService<ITestService>();

            // Should be the same instance
            Assert.Same(singletonFromRoot, singletonFromChild);
            
            // Should still only be tracked in root scope
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(1, container.DisposableContainer.DisposableCount);
        }

        // After child scope disposal, singleton should still be alive
        Assert.False(singletonFromRoot.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(singletonFromRoot.IsDisposed);
    }

    [Fact]
    public void MultipleSingletons_SomeResolvedInRootSomeInChild_ShouldAllBeInRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act - Resolve first singleton in root scope
        var testService = (TestService)container.GetRequiredService<ITestService>();
        Assert.Equal(1, container.DisposableContainer.DisposableCount);

        // Resolve second singleton in child scope
        DependentService dependentService;
        using (var childScope = container.CreateScope())
        {
            dependentService = (DependentService)childScope.ServiceProvider.GetRequiredService<IDependentService>();

            // Both should be in root scope
            Assert.Equal(0, childScope.DisposableContainer.DisposableCount);
            Assert.Equal(2, container.DisposableContainer.DisposableCount);
        }

        // After child scope disposal, both should be alive
        Assert.False(testService.IsDisposed);
        Assert.False(dependentService.IsDisposed);

        // Dispose container
        container.Dispose();
        Assert.True(testService.IsDisposed);
        Assert.True(dependentService.IsDisposed);
    }
}
