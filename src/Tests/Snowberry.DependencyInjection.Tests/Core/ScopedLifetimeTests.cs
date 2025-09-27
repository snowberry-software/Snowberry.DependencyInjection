using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
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
        var service1 = scope.ServiceFactory.GetService<ITestService>();
        var service2 = scope.ServiceFactory.GetService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal(1, scope.DisposableCount);
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
            service1 = scope1.ServiceFactory.GetService<ITestService>();
        }

        using (var scope2 = container.CreateScope())
        {
            service2 = scope2.ServiceFactory.GetService<ITestService>();
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
        var containerService = container.GetService<ITestService>();
        ITestService scopedService;
        using (var scope = container.CreateScope())
        {
            scopedService = scope.ServiceFactory.GetService<ITestService>();
        }

        // Assert
        Assert.NotSame(containerService, scopedService);
        Assert.False(containerService.IsDisposed);
        Assert.True(scopedService.IsDisposed);
        Assert.Equal(1, container.DisposableCount);
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
            service1 = scope.ServiceFactory.GetService<ITestService>();
            service2 = scope.ServiceFactory.GetService<ITestService>();
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
        container.Register(typeof(ITestService), typeof(TestService), null, lifetime, null);

        // Act
        ITestService service;
        using (var scope = container.CreateScope())
        {
            service = scope.ServiceFactory.GetService<ITestService>();

            if (lifetime == ServiceLifetime.Scoped)
            {
                Assert.Same(service, scope.ServiceFactory.GetService<ITestService>());
            }
            else
            {
                Assert.NotSame(service, scope.ServiceFactory.GetService<ITestService>());
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
            var service = scope.ServiceFactory.GetService<ITestService>();
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
            outerService = outerScope.ServiceFactory.GetService<ITestService>();
            outerService.Name = "Outer";

            using (var innerScope = container.CreateScope())
            {
                innerService = innerScope.ServiceFactory.GetService<ITestService>();
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
            scope.ServiceFactory.GetService<ITestService>();
            scope.ServiceFactory.GetService<TestService>();

            Assert.Equal(2, scope.DisposableCount);
        }

        // Assert
        Assert.Equal(2, scope.DisposableCount);
        Assert.True(scope.IsDisposed);
    }
}