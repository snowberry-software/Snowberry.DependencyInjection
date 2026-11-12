using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Concurrency;

/// <summary>
/// Tests that the container stays correct when it holds or creates many services, scopes, and generic
/// closures. Throughput is measured in the benchmark project, not asserted here.
/// </summary>
public class ScaleTests
{
    [Fact]
    public void ServiceRegistration_WithManyKeyedServices_ShouldTrackEveryRegistration()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int serviceCount = 1000;

        // Act
        for (int i = 0; i < serviceCount; i++)
        {
            container.RegisterTransient<ITestService, TestService>($"service_{i}");
        }

        // Assert
        Assert.Equal(serviceCount, container.Count);
    }

    [Fact]
    public void ServiceResolution_WithManyKeyedSingletons_ShouldCreateAndTrackEachOnce()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int serviceCount = 1000;

        for (int i = 0; i < serviceCount; i++)
        {
            container.RegisterSingleton<ITestService, TestService>($"service_{i}");
        }

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredKeyedService<ITestService>($"service_{i}"));
        }

        // Assert
        TestHelpers.AssertAllDistinct(services);
        Assert.Equal(serviceCount, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void ServiceResolution_WithManyTransients_ShouldCreateAndTrackEachInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        const int resolutionCount = 2000;

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < resolutionCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        // Assert
        TestHelpers.AssertAllDistinct(services);
        Assert.Equal(resolutionCount, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void ScopeCreation_WithManyScopes_ShouldDisposeEveryScopedService()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        const int scopeCount = 200;
        var services = new List<ITestService>();

        // Act
        TestHelpers.ForEachScope(container, scopeCount, (scope, _) =>
            services.Add(scope.ServiceProvider.GetRequiredService<ITestService>()));

        // Assert
        TestHelpers.AssertAllDistinct(services);
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void GenericServiceResolution_WithManyTypeArguments_ShouldCloseOpenGenericPerType()
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

        // Act
        var repositories = new List<object>();
        foreach (var type in testTypes)
        {
            repositories.Add(container.GetRequiredService(typeof(IRepository<>).MakeGenericType(type)));
        }

        // Assert
        TestHelpers.AssertAllDistinct(repositories);
        for (int i = 0; i < testTypes.Length; i++)
        {
            Assert.IsType(typeof(Repository<>).MakeGenericType(testTypes[i]), repositories[i]);
        }
    }

    [Fact]
    public void ComplexDependencyResolution_AcrossManyScopes_ShouldDisposeEveryScopedService()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        var services = new List<IComplexService>();

        // Act
        TestHelpers.ForEachScope(container, 200, (scope, _) =>
            services.Add(scope.ServiceProvider.GetRequiredService<IComplexService>()));

        // Assert
        TestHelpers.AssertAllDistinct(services);
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }

    [Fact]
    public void ContainerDisposal_WithManyTransientServices_ShouldDisposeEveryInstance()
    {
        // Arrange
        var container = new ServiceContainer();
        const int serviceCount = 1000;

        container.RegisterTransient<ITestService, TestService>();
        var services = new List<ITestService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        Assert.Equal(serviceCount, container.DisposableContainer.DisposableCount);

        // Act
        container.Dispose();

        // Assert
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }
}
