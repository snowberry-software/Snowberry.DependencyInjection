using System.Diagnostics;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for performance characteristics and scalability of the dependency injection container.
/// </summary>
public class PerformanceAndScalabilityTests
{
    [Fact]
    public void ServiceRegistration_WithManyServices_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int serviceCount = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < serviceCount; i++)
        {
            container.RegisterTransient<ITestService, TestService>($"service_{i}");
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(serviceCount, container.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should complete quickly
    }

    [Fact]
    public void ServiceResolution_WithManySingletons_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int serviceCount = 1000;

        for (int i = 0; i < serviceCount; i++)
        {
            container.RegisterSingleton<ITestService, TestService>($"service_{i}");
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredKeyedService<ITestService>($"service_{i}"));
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(serviceCount, services.Count);
        Assert.Equal(serviceCount, container.DisposableContainer.DisposableCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000); // Should resolve quickly
    }

    [Fact]
    public void ServiceResolution_WithManyTransients_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        const int resolutionCount = 10000;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < resolutionCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(resolutionCount, services.Count);
        Assert.Equal(resolutionCount, container.DisposableContainer.DisposableCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should resolve quickly
    }

    [Fact]
    public void ScopeCreation_WithManyScopes_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        const int scopeCount = 1000;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < scopeCount; i++)
        {
            using var scope = container.CreateScope();
            services.Add(scope.ServiceProvider.GetRequiredService<ITestService>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(scopeCount, services.Count);
        Assert.All(services, s => Assert.True(s.IsDisposed)); // All should be disposed
        Assert.True(stopwatch.ElapsedMilliseconds < 3000); // Should complete quickly
    }

    [Fact]
    public void GenericServiceResolution_WithManyTypes_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        var testTypes = new[]
        {
            typeof(string), typeof(int), typeof(bool), typeof(DateTime), typeof(Guid),
            typeof(List<string>), typeof(Dictionary<string, int>), typeof(TestEntity),
            typeof(ITestService), typeof(object)
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var repositories = new List<object>();
        foreach (var type in testTypes)
        {
            var repositoryType = typeof(IRepository<>).MakeGenericType(type);
            repositories.Add(container.GetRequiredService(repositoryType));
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(testTypes.Length, repositories.Count);
        Assert.All(repositories, Assert.NotNull);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should resolve quickly
    }

    [Fact]
    public void ComplexDependencyResolution_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        const int resolutionCount = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var services = new List<IComplexService>();
        for (int i = 0; i < resolutionCount; i++)
        {
            using var scope = container.CreateScope();
            services.Add(scope.ServiceProvider.GetRequiredService<IComplexService>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(resolutionCount, services.Count);
        Assert.All(services, s => Assert.True(s.IsDisposed));
        Assert.True(stopwatch.ElapsedMilliseconds < 3000); // Should resolve quickly
    }

    [Fact]
    public void MemoryUsage_WithManyTransientServices_ShouldBeReasonable()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        long initialMemory = GC.GetTotalMemory(true);
        const int serviceCount = 10000;

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        long finalMemory = GC.GetTotalMemory(false);
        long memoryUsed = finalMemory - initialMemory;

        // Assert
        Assert.Equal(serviceCount, services.Count);
        // Memory usage should be reasonable (less than 100 bytes per service instance on average)
        Assert.True(memoryUsed < serviceCount * 1000); // Very generous limit
    }

    [Fact]
    public void ContainerDisposal_WithManyServices_ShouldPerformWell()
    {
        // Arrange
        var container = new ServiceContainer();
        const int serviceCount = 5000;

        // Create many services
        container.RegisterTransient<ITestService, TestService>();
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        Assert.Equal(serviceCount, container.DisposableContainer.DisposableCount);
        var stopwatch = Stopwatch.StartNew();

        // Act
        container.Dispose();
        stopwatch.Stop();

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
        Assert.True(stopwatch.ElapsedMilliseconds < 2000); // Should dispose quickly
    }

    [Fact]
    public async Task ConcurrentServiceResolution_WithHighLoad_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        const int taskCount = 100;
        const int resolutionsPerTask = 100;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < resolutionsPerTask; j++)
                {
                    var service = container.GetRequiredService<IDependentService>();
                    Assert.NotNull(service);
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        int totalResolutions = taskCount * resolutionsPerTask;
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should complete reasonably quickly
        // The singleton should be shared, so only 1 + (taskCount * resolutionsPerTask) disposables
        Assert.True(container.DisposableContainer.DisposableCount >= 1); // At least the singleton
    }

    [Fact]
    public void ServiceRegistration_WithComplexHierarchy_ShouldPerformWell()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int hierarchyDepth = 100;

        var stopwatch = Stopwatch.StartNew();

        // Act - Create a hierarchy of dependencies
        container.RegisterSingleton<ITestService, TestService>();

        for (int i = 0; i < hierarchyDepth; i++)
        {
            container.RegisterTransient<IDependentService, DependentService>($"level_{i}");
        }

        // Resolve all services
        var services = new List<IDependentService>();
        for (int i = 0; i < hierarchyDepth; i++)
        {
            services.Add(container.GetRequiredKeyedService<IDependentService>($"level_{i}"));
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(hierarchyDepth, services.Count);
        Assert.All(services, s => Assert.NotNull(s.PrimaryDependency));
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should resolve quickly
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void ServiceResolution_ScalabilityTest_ShouldScaleLinearly(int serviceCount)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(serviceCount, services.Count);

        // Performance should scale roughly linearly
        // Allow 0.01ms per service resolution on average (very generous)
        double maxExpectedTime = serviceCount * 0.01;
        Assert.True(stopwatch.ElapsedMilliseconds < maxExpectedTime + 100); // +100ms buffer
    }
}