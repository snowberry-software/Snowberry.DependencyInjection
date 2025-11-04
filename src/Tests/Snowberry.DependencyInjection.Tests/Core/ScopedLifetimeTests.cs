using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

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
    public void GetService_InDifferentScopes_ShouldReturnDifferentInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        ITestService service1, service2;
        using (var scope1 = container.CreateScope())
        {
            service1 = scope1.ServiceProvider.GetRequiredService<ITestService>();
        }

        using (var scope2 = container.CreateScope())
        {
            service2 = scope2.ServiceProvider.GetRequiredService<ITestService>();
        }

        // Assert
        Assert.NotSame(service1, service2);
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
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
        var scopes = new List<IScope>();

        for (int i = 0; i < 3; i++)
        {
            var scope = container.CreateScope();
            scopes.Add(scope);
            var service = scope.ServiceProvider.GetRequiredService<ITestService>();
            service.Name = $"Service_{i}";
            services.Add(service);
        }

        // Dispose all scopes
        foreach (var scope in scopes)
        {
            scope.Dispose();
        }

        // Assert
        Assert.Equal(3, services.Count);
        for (int i = 0; i < services.Count; i++)
        {
            Assert.Equal($"Service_{i}", services[i].Name);
            Assert.True(services[i].IsDisposed);

            // Verify all services are unique
            for (int j = i + 1; j < services.Count; j++)
            {
                Assert.NotSame(services[i], services[j]);
            }
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
        using var scope = container.CreateScope();
        var dependentService = scope.ServiceProvider.GetRequiredService<IDependentService>();
        var directService = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - ServiceB injected into ServiceA should be the same reference as directly requested ServiceB
        Assert.Same(dependentService.PrimaryDependency, directService);
        Assert.Equal(2, scope.DisposableContainer.DisposableCount); // Only 2 instances should be created (ServiceA and ServiceB)
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
}