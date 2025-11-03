using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for asynchronous disposal functionality including async disposable services,
/// mixed disposal patterns, and proper cleanup in async contexts.
/// </summary>
#if NETCOREAPP
public class AsyncDisposalTests
{
    [Fact]
    public async Task DisposeAsync_WithAsyncDisposableService_ShouldDisposeCorrectly()
    {
        // Arrange
        IAsyncTestService testService;
        var container = new ServiceContainer();
        container.RegisterScoped<IAsyncTestService, AsyncTestService>();

        testService = container.GetRequiredService<IAsyncTestService>();
        testService.Name = "AsyncTest";

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.Equal("AsyncTest", testService.Name);
        Assert.True(testService.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithMixedDisposableTypes_ShouldHandleBoth()
    {
        // Arrange
        IAsyncTestService asyncService;
        ITestService syncService;
        var container = new ServiceContainer();

        container.RegisterSingleton<IAsyncTestService, AsyncTestService>("async");
        container.RegisterSingleton<ITestService, TestService>("sync");

        asyncService = container.GetRequiredKeyedService<IAsyncTestService>("async");
        syncService = container.GetRequiredKeyedService<ITestService>("sync");

        asyncService.Name = "AsyncService";
        syncService.Name = "SyncService";

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(asyncService.IsDisposed);
        Assert.True(syncService.IsDisposed);
        Assert.Equal("AsyncService", asyncService.Name);
        Assert.Equal("SyncService", syncService.Name);
    }

    [Fact]
    public async Task ScopeDisposeAsync_WithAsyncServices_ShouldDisposeOnlyScoped()
    {
        // Arrange
        await using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IAsyncTestService, AsyncTestService>();
        container.RegisterScoped<IAsyncTestService, AsyncDependentService>("dependent");

        var globalService = container.GetRequiredService<IAsyncTestService>();
        globalService.Name = "Global";

        IAsyncTestService scopedService;
        IAsyncTestService scopedDependentService;

        await using (var scope = container.CreateScope())
        {
            scopedService = scope.ServiceFactory.GetRequiredService<IAsyncTestService>();
            scopedDependentService = scope.ServiceFactory.GetRequiredKeyedService<IAsyncTestService>("dependent");

            scopedService.Name = "Scoped";

            Assert.Equal(3, scope.DisposableCount);
        }

        // Assert
        Assert.False(globalService.IsDisposed); // Global should remain
        Assert.True(scopedService.IsDisposed);  // Scoped should be disposed
        Assert.True(scopedDependentService.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithKeyedServices_ShouldDisposeAll()
    {
        // Arrange
        var services = new List<IAsyncTestService>();
        var container = new ServiceContainer();

        for (int i = 0; i < 3; i++)
        {
            string key = $"key_{i}";
            container.RegisterSingleton<IAsyncTestService, AsyncTestService>(key);
            var service = container.GetRequiredKeyedService<IAsyncTestService>(key);
            service.Name = $"Service_{i}";
            services.Add(service);
        }

        Assert.Equal(3, container.DisposableCount);

        // Act
        await container.DisposeAsync();

        // Assert
        for (int i = 0; i < services.Count; i++)
        {
            Assert.True(services[i].IsDisposed);
            Assert.Equal($"Service_{i}", services[i].Name);
        }
    }

    [Fact]
    public async Task DisposeAsync_WithTransientAsyncServices_ShouldDisposeAllInstances()
    {
        // Arrange
        var services = new List<IAsyncTestService>();
        var container = new ServiceContainer();
        container.RegisterTransient<IAsyncTestService, AsyncTestService>();

        for (int i = 0; i < 5; i++)
        {
            var service = container.GetRequiredService<IAsyncTestService>();
            service.Name = $"Transient_{i}";
            services.Add(service);
        }

        Assert.Equal(5, container.DisposableCount);

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
        for (int i = 0; i < services.Count; i++)
        {
            Assert.Equal($"Transient_{i}", services[i].Name);
        }
    }

    [Fact]
    public async Task DisposeAsync_MultipleTimes_ShouldBeSafe()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<IAsyncTestService, AsyncTestService>();
        var service = container.GetRequiredService<IAsyncTestService>();

        // Act
        await container.DisposeAsync();
        await container.DisposeAsync(); // Second disposal should be safe

        // Assert
        Assert.True(service.IsDisposed);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithDependencyChain_ShouldDisposeAll()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IAsyncTestService, AsyncDependentService>();

        IAsyncTestService mainService;
        using (var scope = container.CreateScope())
        {
            mainService = scope.ServiceFactory.GetRequiredService<IAsyncTestService>();

            Assert.NotNull(mainService);

            // Act
            await scope.DisposeAsync();
        }

        // Assert
        Assert.True(mainService.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithFactoryCreatedServices_ShouldDisposeCorrectly()
    {
        // Arrange
        var container = new ServiceContainer();
        int factoryCallCount = 0;

        container.RegisterSingleton<IAsyncTestService>((sp, key) =>
        {
            factoryCallCount++;
            return new AsyncTestService { Name = $"Factory_{factoryCallCount}" };
        });

        var service = container.GetRequiredService<IAsyncTestService>();

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service.IsDisposed);
        Assert.Equal("Factory_1", service.Name);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public async Task DisposeAsync_WithHybridDisposableService_ShouldPreferAsyncDisposal()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, HybridDisposableService>();

        var service = container.GetRequiredService<ITestService>();

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service.IsDisposed);
        // The service should have been disposed via DisposeAsync, not Dispose
        // (HybridDisposableService throws if Dispose is called)
    }

    [Fact]
    public async Task DisposeAsync_PerformanceTest_ShouldHandleManyServices()
    {
        // Arrange
        const int serviceCount = 50; // Reduced for faster test execution
        var container = new ServiceContainer();
        var services = new List<IAsyncTestService>();

        container.RegisterTransient<IAsyncTestService, AsyncTestService>();

        for (int i = 0; i < serviceCount; i++)
        {
            var service = container.GetRequiredService<IAsyncTestService>();
            service.Name = $"Service_{i}";
            services.Add(service);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await container.DisposeAsync();

        stopwatch.Stop();

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
        Assert.True(stopwatch.ElapsedMilliseconds < 4000); // Should complete reasonably quickly
    }
}
#else
public class AsyncDisposalTests
{
    [Fact]
    public void AsyncDisposal_NotAvailableInNetFramework()
    {
        // Placeholder test for .NET Framework where async disposal is not available
        Assert.True(true);
    }
}
#endif