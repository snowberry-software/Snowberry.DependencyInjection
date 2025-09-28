using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for service disposal functionality including disposable tracking,
/// container disposal, and disposal hierarchy management.
/// </summary>
public class ServiceDisposalTests
{
    [Fact]
    public void ContainerDisposal_WithSingletonServices_ShouldDisposeAllServices()
    {
        // Arrange
        TestService service1, service2;
        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterSingleton<TestService>();

            service1 = (TestService)container.GetRequiredService<ITestService>();
            service2 = container.GetRequiredService<TestService>();

            Assert.Equal(2, container.DisposableCount);
            Assert.False(service1.IsDisposed);
            Assert.False(service2.IsDisposed);
        }

        // Assert
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
    }

    [Fact]
    public void ContainerDisposal_WithTransientServices_ShouldDisposeAllInstances()
    {
        // Arrange
        var services = new List<TestService>();
        using (var container = new ServiceContainer())
        {
            container.RegisterTransient<ITestService, TestService>();

            for (int i = 0; i < 5; i++)
            {
                services.Add((TestService)container.GetRequiredService<ITestService>());
            }

            Assert.Equal(5, container.DisposableCount);
            Assert.All(services, s => Assert.False(s.IsDisposed));
        }

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void UserProvidedInstances_ShouldNotBeDisposedByContainer()
    {
        // Arrange
        var userProvidedService = new TestService { Name = "UserProvided" };

        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<ITestService>(userProvidedService);

            var retrievedService = container.GetRequiredService<ITestService>();
            Assert.Same(userProvidedService, retrievedService);
            Assert.Equal(0, container.DisposableCount); // User instances aren't tracked for disposal
        }

        // Assert
        Assert.False(userProvidedService.IsDisposed); // Should not be disposed by container
    }

    [Fact]
    public void ContainerCreatedInstances_ShouldBeDisposedByContainer()
    {
        // Arrange
        TestService containerCreatedService;

        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<ITestService, TestService>();

            containerCreatedService = (TestService)container.GetRequiredService<ITestService>();
            Assert.Equal(1, container.DisposableCount); // Container-created instances are tracked
        }

        // Assert
        Assert.True(containerCreatedService.IsDisposed); // Should be disposed by container
    }

    [Fact]
    public void DisposeContainer_MultipleTimes_ShouldBeSafe()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        var service = (TestService)container.GetRequiredService<ITestService>();

        // Act
        container.Dispose();
        container.Dispose(); // Second disposal should be safe

        // Assert
        Assert.True(service.IsDisposed);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public void DisposedContainer_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(container.GetRequiredService<ITestService>);
        Assert.Throws<ObjectDisposedException>(() => container.RegisterSingleton<TestService>());
        Assert.Throws<ObjectDisposedException>(container.CreateScope);
    }

    [Fact]
    public void DisposableCount_ShouldTrackCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Equal(0, container.DisposableCount);

        container.RegisterSingleton<ITestService, TestService>();
        Assert.Equal(0, container.DisposableCount); // Not created yet

        container.GetRequiredService<ITestService>();
        Assert.Equal(1, container.DisposableCount); // Now created

        container.RegisterTransient<TestService>();
        container.GetRequiredService<TestService>();
        Assert.Equal(2, container.DisposableCount);

        container.GetRequiredService<TestService>(); // Another transient
        Assert.Equal(3, container.DisposableCount);
    }

    [Fact]
    public void UnregisterService_ShouldDisposeRegisteredInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        var service = (TestService)container.GetRequiredService<ITestService>();

        Assert.Equal(1, container.DisposableCount);
        Assert.False(service.IsDisposed);

        // Act
        container.UnregisterService<ITestService>(null, out bool successful);

        // Assert
        Assert.True(successful);
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public void UnregisterService_WithUserProvidedInstance_ShouldStillDispose()
    {
        // Arrange
        using var container = new ServiceContainer();
        var userInstance = new TestService();
        container.RegisterSingleton<ITestService>(userInstance);

        Assert.Equal(0, container.DisposableCount); // User instances not tracked for container disposal
        Assert.False(userInstance.IsDisposed);

        // Act
        container.UnregisterService<ITestService>(null, out bool successful);

        // Assert
        Assert.True(successful);
        Assert.True(userInstance.IsDisposed); // But still disposed on unregister
    }

    [Fact]
    public void DisposalOrder_ShouldNotMatter()
    {
        // Arrange
        var services = new List<TestService>();
        using (var container = new ServiceContainer())
        {
            // Register services in different orders
            container.RegisterSingleton<TestService>("service1");
            container.RegisterTransient<TestService>("service2");
            container.RegisterSingleton<TestService>("service3");

            services.Add(container.GetRequiredKeyedService<TestService>("service1"));
            services.Add(container.GetRequiredKeyedService<TestService>("service2"));
            services.Add(container.GetRequiredKeyedService<TestService>("service3"));

            Assert.Equal(3, container.DisposableCount);
        }

        // Assert - All should be disposed regardless of registration order
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void ScopeDisposal_ShouldOnlyDisposeServicesInScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        var globalService = (TestService)container.GetRequiredService<ITestService>();
        TestService scopedService;

        using (var scope = container.CreateScope())
        {
            scopedService = (TestService)scope.ServiceFactory.GetRequiredService<ITestService>();

            Assert.NotSame(globalService, scopedService);
            Assert.False(globalService.IsDisposed);
            Assert.False(scopedService.IsDisposed);
        }

        // Assert
        Assert.False(globalService.IsDisposed); // Global service should remain
        Assert.True(scopedService.IsDisposed);  // Scoped service should be disposed
    }

    [Fact]
    public void DisposalWithDependencies_ShouldDisposeAllRelatedServices()
    {
        // Arrange
        TestService testService;
        DependentService dependentService;

        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<ITestService, TestService>();
            container.RegisterSingleton<IDependentService, DependentService>();

            dependentService = (DependentService)container.GetRequiredService<IDependentService>();
            testService = (TestService)dependentService.PrimaryDependency;

            Assert.Equal(2, container.DisposableCount);
        }

        // Assert
        Assert.True(testService.IsDisposed);
        Assert.True(dependentService.IsDisposed);
    }
}