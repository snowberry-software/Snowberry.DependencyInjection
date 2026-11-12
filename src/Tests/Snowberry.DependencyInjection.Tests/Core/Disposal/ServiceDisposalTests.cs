using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Disposal;

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

            Assert.Equal(2, container.DisposableContainer.DisposableCount);
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

            Assert.Equal(5, container.DisposableContainer.DisposableCount);
            Assert.All(services, s => Assert.False(s.IsDisposed));
        }

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
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
        Assert.Equal(0, container.DisposableContainer.DisposableCount);

        container.RegisterSingleton<ITestService, TestService>();
        Assert.Equal(0, container.DisposableContainer.DisposableCount); // Not created yet

        container.GetRequiredService<ITestService>();
        Assert.Equal(1, container.DisposableContainer.DisposableCount); // Now created

        container.RegisterTransient<TestService>();
        container.GetRequiredService<TestService>();
        Assert.Equal(2, container.DisposableContainer.DisposableCount);

        container.GetRequiredService<TestService>(); // Another transient
        Assert.Equal(3, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void UnregisterService_ShouldDisposeRegisteredInstance()
    {
        // Arrange
        using var container = TestHelpers.CreateMutableContainer();
        container.RegisterSingleton<ITestService, TestService>();
        var service = (TestService)container.GetRequiredService<ITestService>();

        Assert.Equal(1, container.DisposableContainer.DisposableCount);
        Assert.False(service.IsDisposed);

        // Act
        container.UnregisterService<ITestService>(null, out bool successful);

        // Assert
        Assert.True(successful);
        Assert.True(service.IsDisposed);
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

            Assert.Equal(3, container.DisposableContainer.DisposableCount);
        }

        // Assert - All should be disposed regardless of registration order
        Assert.All(services, s => Assert.True(s.IsDisposed));
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

            Assert.Equal(2, container.DisposableContainer.DisposableCount);
        }

        // Assert
        Assert.True(testService.IsDisposed);
        Assert.True(dependentService.IsDisposed);
    }
}