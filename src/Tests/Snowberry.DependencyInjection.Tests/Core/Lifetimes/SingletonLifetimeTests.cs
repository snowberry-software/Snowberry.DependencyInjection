using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Lifetimes;

/// <summary>
/// Tests for singleton service lifetime behavior including instance reuse,
/// factory methods, and the scope that singletons receive.
/// </summary>
public class SingletonLifetimeTests
{
    [Fact]
    public void GetService_WithSingletonRegistration_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithSingletonInstance_ShouldReturnProvidedInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        var providedInstance = new TestService { Name = "ProvidedInstance" };
        container.RegisterSingleton<ITestService>(providedInstance);

        // Act
        var retrievedService = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(providedInstance, retrievedService);
        Assert.Equal("ProvidedInstance", retrievedService.Name);
        Assert.Equal(0, container.DisposableContainer.DisposableCount); // User-provided instances are not tracked for disposal
    }

    [Fact]
    public void GetService_WithSingletonFactory_ShouldUseSameFactoryInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        int callCount = 0;
        container.RegisterSingleton<ITestService>((sp, key) =>
        {
            callCount++;
            return new TestService { Name = $"Factory_{callCount}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Equal("Factory_1", service1.Name);
        Assert.Equal(1, callCount); // Factory should only be called once
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithSingletonConcreteType_ShouldReturnSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<TestService>();

        // Act
        var service1 = container.GetRequiredService<TestService>();
        var service2 = container.GetRequiredService<TestService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void GetService_WithSingletonAndCustomFactory_ShouldPassCorrectParameters()
    {
        // Arrange
        using var container = new ServiceContainer();
        object? capturedServiceKey = null;

        container.RegisterSingleton<ITestService>((provider, serviceKey) =>
        {
            capturedServiceKey = serviceKey;
            return new TestService { Name = "FactoryTest" };
        });

        // Act
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Null(capturedServiceKey);
        Assert.Equal("FactoryTest", service.Name);
    }

    [Fact]
    public void MultipleScopes_ShouldReturnSameInstanceAndReceiveRootScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        var collectedServices = new List<ServiceWithScopeAndProviderDependencies>();
        var collectedTestServices = new List<ITestService>();

        // Act - the singletons are first created inside the first child scope
        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();

            // Resolving through the constructor-injected provider must yield the same singletons
            Assert.Same(testService, service.ServiceProvider.GetRequiredService<ITestService>());
            Assert.Same(service, service.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>());

            // A singleton always receives the root scope, even when first resolved from a child scope
            Assert.True(service.Scope.IsGlobalScope);

            collectedServices.Add(service);
            collectedTestServices.Add(testService);
        });

        // Assert
        Assert.All(collectedServices, service => Assert.Same(collectedServices[0], service));
        Assert.All(collectedTestServices, testService => Assert.Same(collectedTestServices[0], testService));

        // Singletons outlive the child scopes and die with the container
        Assert.False(collectedServices[0].IsDisposed);
        Assert.False(collectedTestServices[0].IsDisposed);

        container.Dispose();

        Assert.True(collectedServices[0].IsDisposed);
        Assert.True(collectedTestServices[0].IsDisposed);
    }

    [Fact]
    public void ResolvedFromRootThenChildScopes_ShouldReceiveRootScopeAndSameInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        // Act
        var serviceFromRoot = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // Assert
        Assert.True(serviceFromRoot.Scope.IsGlobalScope);

        TestHelpers.ForEachScope(container, 3, (scope, _) =>
        {
            var serviceFromChild = scope.ServiceProvider.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

            Assert.Same(serviceFromRoot, serviceFromChild);
            Assert.True(serviceFromChild.Scope.IsGlobalScope);
        });

        Assert.False(serviceFromRoot.IsDisposed);
    }
}
