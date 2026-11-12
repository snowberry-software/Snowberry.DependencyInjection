using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Lifetimes;

/// <summary>
/// Tests for scoped service lifetime behavior including scope isolation,
/// disposal patterns, and service sharing within scope boundaries.
/// </summary>
public class ScopedLifetimeTests
{
    [Fact]
    public void GetService_WithinSameScope_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal(1, scope.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_InContainerScope_ShouldIsolateScopedServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        var containerService = container.GetRequiredService<ITestService>();
        ITestService scopedService;
        using (var scope = container.CreateScope())
        {
            scopedService = scope.ServiceProvider.GetRequiredService<ITestService>();
        }

        // Assert
        Assert.NotSame(containerService, scopedService);
        Assert.False(containerService.IsDisposed);
        Assert.True(scopedService.IsDisposed);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithScopedFactory_ShouldCallFactoryOncePerScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;
        container.RegisterScoped<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new TestService { Name = $"Scoped_{factoryCallCount}" };
        });

        // Act
        ITestService service1, service2;
        using (var scope = container.CreateScope())
        {
            service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
            service2 = scope.ServiceProvider.GetRequiredService<ITestService>();
        }

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal("Scoped_1", service1.Name);
        Assert.Equal(1, factoryCallCount);
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void DisposeScope_WithDifferentLifetimes_ShouldDisposeCorrectly(ServiceLifetime lifetime)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(new ServiceDescriptor(typeof(ITestService), typeof(TestService), lifetime, singletonInstance: null));

        // Act
        ITestService service;
        using (var scope = container.CreateScope())
        {
            service = scope.ServiceProvider.GetRequiredService<ITestService>();

            if (lifetime == ServiceLifetime.Scoped)
            {
                Assert.Same(service, scope.ServiceProvider.GetRequiredService<ITestService>());
            }
            else
            {
                Assert.NotSame(service, scope.ServiceProvider.GetRequiredService<ITestService>());
            }
        }

        // Assert
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public void CreateMultipleScopes_ShouldIsolateServices()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        var services = new List<ITestService>();
        TestHelpers.ForEachScope(container, 3, (scope, i) =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ITestService>();
            service.Name = $"Service_{i}";
            services.Add(service);
        });

        // Assert
        Assert.Equal(3, services.Count);
        TestHelpers.AssertAllDistinct(services);

        for (int i = 0; i < services.Count; i++)
        {
            Assert.Equal($"Service_{i}", services[i].Name);
            Assert.True(services[i].IsDisposed);
        }
    }

    [Fact]
    public void NestedScopes_ShouldMaintainIsolation()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        ITestService outerService, innerService;
        using (var outerScope = container.CreateScope())
        {
            outerService = outerScope.ServiceProvider.GetRequiredService<ITestService>();
            outerService.Name = "Outer";

            using (var innerScope = container.CreateScope())
            {
                innerService = innerScope.ServiceProvider.GetRequiredService<ITestService>();
                innerService.Name = "Inner";

                // Services should be different
                Assert.NotSame(outerService, innerService);
                Assert.Equal("Outer", outerService.Name);
                Assert.Equal("Inner", innerService.Name);
            }

            // Inner service should be disposed, outer should not
            Assert.True(innerService.IsDisposed);
            Assert.False(outerService.IsDisposed);
        }

        // Both services should now be disposed
        Assert.True(outerService.IsDisposed);
        Assert.True(innerService.IsDisposed);
    }

    [Fact]
    public void ScopeDisposal_ShouldTrackDisposableCount()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<TestService>();

        // Act
        IScope scope;
        using (scope = container.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITestService>();
            scope.ServiceProvider.GetRequiredService<TestService>();

            Assert.Equal(2, scope.DisposableContainer.DisposableCount);
        }

        // Assert
        Assert.Equal(2, scope.DisposableContainer.DisposableCount);
        Assert.True(scope.IsDisposed);
    }

    [Fact]
    public void ScopedDependency_ResolvedInSameScope_ShouldShareSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();

        // Act
        IDependentService dependentService;
        ITestService directService;
        using (var scope = container.CreateScope())
        {
            dependentService = scope.ServiceProvider.GetRequiredService<IDependentService>();
            directService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Assert - the dependency injected into the consumer is the instance requested directly
            Assert.Same(dependentService.PrimaryDependency, directService);
            Assert.Equal(2, scope.DisposableContainer.DisposableCount); // Only the consumer and its dependency were created
        }

        // Everything created in the scope is disposed with it
        Assert.True(dependentService.IsDisposed);
        Assert.True(directService.IsDisposed);
    }

    [Fact]
    public void ScopedDependency_ResolvedViaIServiceProvider_ShouldShareSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<ServiceWithServiceProviderDependency>();

        // Act
        using var scope = container.CreateScope();
        var serviceWithProvider = scope.ServiceProvider.GetRequiredService<ServiceWithServiceProviderDependency>();
        var directService = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - ServiceB resolved via IServiceProvider.GetRequiredService<T>() should be the same reference as directly requested ServiceB
        Assert.Same(serviceWithProvider.ResolvedService, directService);
        Assert.Equal(2, scope.DisposableContainer.DisposableCount); // Only 2 instances should be created (ServiceWithProvider and TestService)
    }

    [Fact]
    public void MultipleScopes_ShouldReturnDifferentInstancesPerScopeAndReceiveCurrentScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<ServiceWithScopeAndProviderDependencies>();

        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();

        // Act
        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Scoped services are shared within the scope, also through the constructor-injected provider
            Assert.Same(service, scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>());
            Assert.Same(testService, scope.ServiceProvider.GetRequiredService<ITestService>());
            Assert.Same(testService, service.ServiceProvider.GetRequiredService<ITestService>());

            // Each scoped service receives the scope it was resolved from
            Assert.Same(scope.ServiceProvider, service.ServiceProvider);
            Assert.Same(scope, service.Scope);
            Assert.False(service.Scope.IsGlobalScope);

            Assert.False(service.IsDisposed);
            Assert.False(testService.IsDisposed);

            collectedServices.Add(service);
            collectedTestServices.Add(testService);
        });

        // Assert
        TestHelpers.AssertAllDistinct(collectedServices);
        TestHelpers.AssertAllDistinct(collectedTestServices);
        Assert.All(collectedServices, service => Assert.True(service.IsDisposed));
        Assert.All(collectedTestServices, testService => Assert.True(testService.IsDisposed));
    }

    [Fact]
    public void ResolvedFromRootScope_ShouldBeGlobalScopeAndDifferFromChildScopes()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithScopeAndProviderDependencies>();

        // Act - the root scope is treated as a scope of its own
        var serviceFromRoot = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // Assert
        Assert.True(serviceFromRoot.Scope.IsGlobalScope);

        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var serviceFromChild = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            Assert.NotSame(serviceFromRoot, serviceFromChild);
            Assert.False(serviceFromChild.Scope.IsGlobalScope);
        });
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

        // Act
        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var singleton = scope.ServiceProvider.GetRequiredService<ITestService>();
            var transient = scope.ServiceProvider.GetRequiredService<IDependentService>();
            var scoped = scope.ServiceProvider.GetRequiredService<IComplexService>();

            singletonInstance ??= singleton;

            Assert.Same(singletonInstance, singleton);
            Assert.NotSame(transient, scope.ServiceProvider.GetRequiredService<IDependentService>());
            Assert.Same(scoped, scope.ServiceProvider.GetRequiredService<IComplexService>());

            // Transient and scoped consumers share the singleton dependency
            Assert.Same(singletonInstance, transient.PrimaryDependency);
            Assert.Same(singletonInstance, scoped.TestService);

            transientInstances.Add(transient);
            scopedInstances.Add(scoped);
        });

        // Assert
        TestHelpers.AssertAllDistinct(transientInstances);
        TestHelpers.AssertAllDistinct(scopedInstances);

        Assert.False(singletonInstance!.IsDisposed);
        Assert.All(transientInstances, t => Assert.True(t.IsDisposed));
        Assert.All(scopedInstances, s => Assert.True(s.IsDisposed));

        container.Dispose();
        Assert.True(singletonInstance.IsDisposed);
    }
}
