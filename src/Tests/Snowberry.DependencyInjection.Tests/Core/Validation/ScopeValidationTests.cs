using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Validation;

/// <summary>
/// Tests for the ValidateScopes option that ensures scoped services 
/// can only be resolved from child scopes and not from the root container.
/// </summary>
public class ScopeValidationTests
{
    /// <summary>
    /// Creates a container with <see cref="ServiceContainerOptions.ValidateScopes"/> enabled and
    /// <see cref="ITestService"/> registered as scoped.
    /// </summary>
    /// <returns>A new validating container.</returns>
    private static ServiceContainer CreateValidatingContainer()
    {
        var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService, TestService>();
        return container;
    }

    /// <summary>
    /// Scoped resolution scenarios: a name, extra registrations, how to resolve, the service type reported
    /// on failure, and the implementation type expected on success.
    /// </summary>
    public static TheoryData<string, Action<ServiceContainer>, Func<IKeyedServiceProvider, object?>, Type, Type> ScopedResolutions => new()
    {
        { "plain", static _ => { }, static sp => sp.GetRequiredService<ITestService>(), typeof(ITestService), typeof(TestService) },
        { "keyed", static c => c.RegisterScoped<ITestService, TestService>("my-key"), static sp => sp.GetRequiredKeyedService<ITestService>("my-key"), typeof(ITestService), typeof(TestService) },
        { "factory", static c => c.RegisterScoped<TestService>((sp, key) => new TestService { Name = "Factory" }), static sp => sp.GetRequiredService<TestService>(), typeof(TestService), typeof(TestService) },
        { "generic", static c => c.RegisterScoped<IRepository<TestService>, Repository<TestService>>(), static sp => sp.GetRequiredService<IRepository<TestService>>(), typeof(IRepository<TestService>), typeof(Repository<TestService>) },
        { "GetService(Type)", static _ => { }, static sp => sp.GetService(typeof(ITestService)), typeof(ITestService), typeof(TestService) },
        { "GetKeyedService(Type, key)", static c => c.RegisterScoped<ITestService, TestService>("key"), static sp => sp.GetKeyedService(typeof(ITestService), "key"), typeof(ITestService), typeof(TestService) },
    };

    // The case name only labels the row; each theory uses the subset of the shared row data it needs.
#pragma warning disable xUnit1026
    [Theory]
    [MemberData(nameof(ScopedResolutions))]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromRoot_ShouldThrow(
        string caseName, Action<ServiceContainer> register, Func<IKeyedServiceProvider, object?> resolve, Type serviceType, Type implementationType)
    {
        // Arrange
        using var container = CreateValidatingContainer();
        register(container);

        // Act & Assert
        var exception = Assert.Throws<ServiceScopeRequiredException>(() => resolve(container));

        Assert.Equal(serviceType, exception.ServiceType);
        Assert.True(container.ValidateScopes);
    }

    [Theory]
    [MemberData(nameof(ScopedResolutions))]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromChildScope_ShouldSucceed(
        string caseName, Action<ServiceContainer> register, Func<IKeyedServiceProvider, object?> resolve, Type serviceType, Type implementationType)
    {
        // Arrange
        using var container = CreateValidatingContainer();
        register(container);

        // Act
        using var scope = container.CreateScope();
        var service = resolve(scope.ServiceProvider);

        // Assert
        Assert.IsType(implementationType, service);
    }
#pragma warning restore xUnit1026

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    public void ValidateScopes_Enabled_NonScopedServiceResolvedFromRoot_ShouldSucceed(ServiceLifetime lifetime)
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        if (lifetime == ServiceLifetime.Singleton)
            container.RegisterSingleton<ITestService, TestService>();
        else
            container.RegisterTransient<ITestService, TestService>();

        // Act - Singletons and transients should work fine from root
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceWithFactoryResolvedFromChildScope_ShouldUseFactory()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterScoped<ITestService>((sp, key) => new TestService { Name = "Factory" });

        // Act
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert
        Assert.Equal("Factory", service.Name);
    }

    [Fact]
    public void ValidateScopes_Disabled_ScopedServiceResolvedFromRoot_ShouldSucceed()
    {
        // Arrange - ValidateScopes is NOT enabled (default behavior)
        using var container = new ServiceContainer(ServiceContainerOptions.Default);
        container.RegisterScoped<ITestService, TestService>();

        // Act - Should succeed without validation
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.IsType<TestService>(service);
        Assert.False(container.ValidateScopes);
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceWithDependenciesResolvedFromRoot_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();

        // Act & Assert - Should throw for the scoped service
        var exception = Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IDependentService>());

        Assert.Equal(typeof(IDependentService), exception.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Enabled_SingletonDependingOnScoped_ThrowsBecauseTheScopedServiceWouldBeCaptured()
    {
        // Arrange: singletons are constructed in the root scope, so their scoped dependency would live as long as
        // the container.
        using var container = CreateValidatingContainer();
        container.RegisterSingleton<IDependentService, DependentService>();
        using var scope = container.CreateScope();

        // Act & Assert: rejected from the root and from a child scope alike.
        var fromRoot = Assert.Throws<ServiceScopeRequiredException>(() => container.GetService<IDependentService>());
        Assert.Throws<ServiceScopeRequiredException>(() => scope.ServiceProvider.GetService<IDependentService>());
        Assert.Equal(typeof(ITestService), fromRoot.ServiceType);
    }

    [Fact]
    public void ValidateScopes_Disabled_SingletonDependingOnScoped_CapturesARootScopedInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        using var scope = container.CreateScope();

        // Act
        var service = container.GetRequiredService<IDependentService>();

        // Assert: the captured dependency is the root scope's instance, not the child scope's.
        Assert.Same(container.GetRequiredService<ITestService>(), service.PrimaryDependency);
        Assert.NotSame(scope.ServiceProvider.GetRequiredService<ITestService>(), service.PrimaryDependency);
    }

    [Fact]
    public void ValidateScopes_Enabled_MultipleScopedServicesResolvedFromRoot_ShouldAllThrow()
    {
        // Arrange
        using var container = CreateValidatingContainer();
        container.RegisterScoped<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        // Act & Assert - All should throw
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IDependentService>());
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IComplexService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedFromNestedScope_ShouldSucceed()
    {
        // Arrange
        using var container = CreateValidatingContainer();

        // Act - Resolve from nested scopes
        using var outerScope = container.CreateScope();
        var outerService = outerScope.ServiceProvider.GetRequiredService<ITestService>();

        using var innerScope = container.CreateScope();
        var innerService = innerScope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - Both should succeed and be different instances
        Assert.NotSame(outerService, innerService);
    }

    [Fact]
    public void ValidateScopes_EnabledWithReadOnly_ShouldWorkTogether()
    {
        // Arrange
        using var container = new ServiceContainer(
            ServiceContainerOptions.ValidateScopes | ServiceContainerOptions.ReadOnly);
        container.RegisterScoped<ITestService, TestService>();

        // Assert - Both options should be enabled
        Assert.True(container.ValidateScopes);
        Assert.True(container.AreRegisteredServicesReadOnly);

        // Act & Assert - Scoped validation should work
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<ITestService>());

        // Act & Assert - ReadOnly should work
        Assert.Throws<ServiceRegistryReadOnlyException>(() =>
            container.RegisterScoped<ITestService, TestService>());
    }

    [Fact]
    public void ValidateScopes_Enabled_MixedLifetimesResolvedFromRoot_OnlyScopedShouldThrow()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        // Act & Assert - Singleton should work
        var singleton = container.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(singleton);

        // Act & Assert - Transient should work
        var transient = container.GetRequiredService<IDependentService>();
        Assert.IsType<DependentService>(transient);

        // Act & Assert - Scoped should throw
        Assert.Throws<ServiceScopeRequiredException>(() =>
            container.GetRequiredService<IComplexService>());
    }

    [Fact]
    public void ValidateScopes_CheckPropertyValue_ShouldReflectOption()
    {
        // Arrange & Act
        using var containerWithValidation = new ServiceContainer(ServiceContainerOptions.ValidateScopes);
        using var containerWithoutValidation = new ServiceContainer(ServiceContainerOptions.None);
        using var containerDefault = new ServiceContainer();

        // Assert
        Assert.True(containerWithValidation.ValidateScopes);
        Assert.False(containerWithoutValidation.ValidateScopes);
        Assert.False(containerDefault.ValidateScopes); // Default does not include ValidateScopes
    }

    [Fact]
    public void ValidateScopes_Enabled_ResolveScopedViaInjectedServiceProviderFromRoot_ShouldStillThrow()
    {
        // Arrange
        using var container = CreateValidatingContainer();
        container.RegisterSingleton<ServiceWithScopeAndProviderDependencies>();

        // Act - Singleton gets injected with root IServiceProvider
        var singletonService = container.GetRequiredService<ServiceWithScopeAndProviderDependencies>();

        // The singleton was created successfully (it receives root ServiceProvider)
        Assert.NotNull(singletonService.ServiceProvider);
        Assert.True(singletonService.Scope.IsGlobalScope);

        // Act & Assert - But trying to resolve scoped service from that injected provider should throw
        // because it's the root scope's ServiceProvider
        Assert.Throws<ServiceScopeRequiredException>(() =>
            singletonService.ServiceProvider.GetRequiredService<ITestService>());
    }

    [Fact]
    public async Task ValidateScopes_Enabled_MultipleThreadsResolvingScopedFromRoot_AllShouldThrow()
    {
        // Arrange
        using var container = CreateValidatingContainer();

        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act - Try to resolve from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    container.GetRequiredService<ITestService>();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All should have thrown ServiceScopeRequiredException
        Assert.Equal(10, exceptions.Count);
        Assert.All(exceptions, ex => Assert.IsType<ServiceScopeRequiredException>(ex));
    }

    [Fact]
    public void ValidateScopes_Enabled_ScopedServiceResolvedMultipleTimesFromSameChildScope_ShouldSucceed()
    {
        // Arrange
        using var container = CreateValidatingContainer();

        // Act
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service3 = scope.ServiceProvider.GetRequiredService<ITestService>();

        // Assert - All should be the same instance (scoped behavior)
        Assert.Same(service1, service2);
        Assert.Same(service2, service3);
    }
}
