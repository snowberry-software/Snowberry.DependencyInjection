using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Tests for dependency injection functionality including constructor injection,
/// property injection, and handling of missing dependencies.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void ConstructorInjection_WithRegisteredDependency_ShouldInjectSuccessfully()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act
        var service = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.IsType<TestService>(service.PrimaryDependency);

        // The [Inject] property receives the same singleton as the constructor parameter
        Assert.Same(service.PrimaryDependency, service.OptionalDependency);
        Assert.Equal(2, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void ConstructorInjection_WithMissingDependency_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act & Assert
        var exception = Assert.Throws<ServiceTypeNotRegistered>(container.GetRequiredService<IDependentService>);

        Assert.Contains(typeof(ITestService).Name, exception.Message);
        Assert.Equal(0, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void DependencyInjection_WithTransientServices_ShouldCreateNewInstancesForEachDependency()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        // Act
        var service1 = container.GetRequiredService<IDependentService>();
        var service2 = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.NotSame(service1.PrimaryDependency, service2.PrimaryDependency);

        Assert.Equal(6, container.DisposableContainer.DisposableCount); // 2 main services + 4 injected dependencies
    }

    [Fact]
    public void DependencyInjection_WithMixedLifetimes_ShouldFollowCorrectRules()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>(); // Singleton dependency
        container.RegisterTransient<IDependentService, DependentService>(); // Transient consumer

        // Act
        var service1 = container.GetRequiredService<IDependentService>();
        var service2 = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.NotSame(service1, service2); // Different transient instances
        Assert.Same(service1.PrimaryDependency, service2.PrimaryDependency); // Same singleton dependency

        Assert.Equal(3, container.DisposableContainer.DisposableCount); // 2 transient services + 1 singleton
    }

    [Fact]
    public void DependencyInjection_WithComplexDependencyChain_ShouldResolveCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<IComplexService, ComplexService>();

        // Act
        var service = container.GetRequiredService<IComplexService>();

        // Assert

        // All dependencies should be the same singleton instances
        Assert.Same(service.TestService, service.DependentService.PrimaryDependency);

        Assert.Equal(3, container.DisposableContainer.DisposableCount); // 3 different service types
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void DependencyInjection_WithMultipleDependentServices_ShouldInjectCorrectly(int serviceCount)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        // Act
        var services = new List<IDependentService>();
        for (int i = 0; i < serviceCount; i++)
        {
            services.Add(container.GetRequiredService<IDependentService>());
        }

        // Assert
        Assert.Equal(serviceCount, services.Count);

        // All services should have the same singleton dependency injected
        var firstServiceDependency = services[0].PrimaryDependency;
        Assert.All(services, service =>
        {
            Assert.Same(firstServiceDependency, service.PrimaryDependency);
        });

        // Should have serviceCount transient services + 1 singleton dependency
        Assert.Equal(serviceCount + 1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void DependencyInjection_WithServiceFactory_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) => new TestService { Name = "FactoryCreated" });
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act
        var service = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.Equal("FactoryCreated", service.PrimaryDependency.Name);
    }

    [Fact]
    public void DependencyInjection_WithAlternativeImplementations_ShouldUseRegisteredType()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, AlternativeTestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act
        var service = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.IsType<AlternativeTestService>(service.PrimaryDependency);
        Assert.Equal("AlternativeTestService", service.PrimaryDependency.Name);
    }
}