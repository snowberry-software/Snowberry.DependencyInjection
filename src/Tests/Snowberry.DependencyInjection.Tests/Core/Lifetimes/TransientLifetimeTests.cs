using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Lifetimes;

/// <summary>
/// Tests for transient service lifetime behavior including instance creation,
/// factory methods, and the scope that transients receive.
/// </summary>
public class TransientLifetimeTests
{
    [Fact]
    public void GetService_WithTransientRegistration_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.IsType<TestService>(service2);
        Assert.Equal(2, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientConcreteType_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<TestService>();

        // Act
        var service1 = container.GetRequiredService<TestService>();
        var service2 = container.GetRequiredService<TestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.IsType<TestService>(service2);
        Assert.Equal(2, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientFactory_ShouldCallFactoryEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        int callCount = 0;
        container.RegisterTransient<ITestService>((sp, key) =>
        {
            callCount++;
            return new TestService { Name = $"Instance_{callCount}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.Equal("Instance_1", service1.Name);
        Assert.Equal("Instance_2", service2.Name);
        Assert.Equal(2, callCount);
        Assert.Equal(2, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientAndModification_ShouldNotAffectOtherInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        service1.Name = "Modified";

        // Assert
        Assert.Equal("Modified", service1.Name);
        Assert.Equal("DefaultTestService", service2.Name);
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void GetService_WithTransientFactoryParameters_ShouldPassCorrectValues()
    {
        // Arrange
        using var container = new ServiceContainer();
        var capturedKeys = new List<object?>();

        container.RegisterTransient<ITestService>((provider, serviceKey) =>
        {
            capturedKeys.Add(serviceKey);
            return new TestService { Name = $"Call_{capturedKeys.Count}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Equal(2, capturedKeys.Count);
        Assert.All(capturedKeys, Assert.Null);
        Assert.Equal("Call_1", service1.Name);
        Assert.Equal("Call_2", service2.Name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetService_WithMultipleTransientCalls_ShouldTrackAllInstances(int instanceCount)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < instanceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        // Assert
        Assert.Equal(instanceCount, container.DisposableContainer.DisposableCount);
        Assert.Equal(instanceCount, services.Count);
        TestHelpers.AssertAllDistinct(services);
    }

    [Fact]
    public void MultipleScopes_ShouldReturnDifferentInstancesAndReceiveCurrentScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        container.RegisterTransient<ServiceWithScopeAndProviderDependencies>();

        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();
        var childResolvedServices = new List<ITestService>();

        // Act
        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Resolve through the constructor-injected provider
            var childTestService = service.ServiceProvider.GetRequiredService<ITestService>();

            // Each transient receives the scope it was resolved from
            Assert.Same(scope.ServiceProvider, service.ServiceProvider);
            Assert.Same(scope, service.Scope);
            Assert.False(service.Scope.IsGlobalScope);

            Assert.NotSame(testService, childTestService);

            Assert.False(service.IsDisposed);
            Assert.False(testService.IsDisposed);
            Assert.False(childTestService.IsDisposed);

            collectedServices.Add(service);
            collectedTestServices.Add(testService);
            childResolvedServices.Add(childTestService);
        });

        // Assert
        TestHelpers.AssertAllDistinct(collectedServices);
        TestHelpers.AssertAllDistinct(collectedTestServices);
        TestHelpers.AssertAllDistinct(childResolvedServices);

        // Transients are disposed with the scope that created them
        Assert.All(collectedServices, service => Assert.True(service.IsDisposed));
        Assert.All(collectedTestServices, testService => Assert.True(testService.IsDisposed));
        Assert.All(childResolvedServices, testService => Assert.True(testService.IsDisposed));
    }

    [Fact]
    public void ResolvedMultipleTimesInSameScope_ShouldCreateNewInstancesWithSameScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ServiceWithScopeAndProviderDependencies>();

        // Act & Assert
        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var instances = new List<ServiceWithScopeAndProviderDependencies>();

            for (int i = 0; i < 3; i++)
            {
                var instance = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
                instances.Add(instance);

                Assert.Same(scope.ServiceProvider, instance.ServiceProvider);
                Assert.Same(scope, instance.Scope);
                Assert.False(instance.Scope.IsGlobalScope);
            }

            TestHelpers.AssertAllDistinct(instances);
        });
    }
}
