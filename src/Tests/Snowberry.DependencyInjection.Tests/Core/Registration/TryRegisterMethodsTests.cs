using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Registration;

/// <summary>
/// Tests for TryRegister extension methods including TryRegisterSingleton, TryRegisterTransient, and TryRegisterScoped.
/// Verifies that these methods correctly return true when registration succeeds and false when service already exists.
/// </summary>
public class TryRegisterMethodsTests
{
    #region Helpers

    /// <summary>
    /// Calls the TryRegister method that matches <paramref name="lifetime"/>.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImpl">The implementation type.</typeparam>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime to register with.</param>
    /// <param name="key">The optional service key.</param>
    /// <returns>The result of the TryRegister call.</returns>
    private static bool TryRegisterWithLifetime<TService, TImpl>(ServiceContainer container, ServiceLifetime lifetime, string? key)
        where TImpl : TService
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => container.TryRegisterSingleton<TService, TImpl>(key),
            ServiceLifetime.Transient => container.TryRegisterTransient<TService, TImpl>(key),
            ServiceLifetime.Scoped => container.TryRegisterScoped<TService, TImpl>(key),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
        };
    }

    /// <summary>
    /// Calls the Register method that matches <paramref name="lifetime"/>.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <typeparam name="TImpl">The implementation type.</typeparam>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime to register with.</param>
    /// <param name="key">The optional service key.</param>
    private static void RegisterWithLifetime<TService, TImpl>(ServiceContainer container, ServiceLifetime lifetime, string? key)
        where TImpl : TService
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                container.RegisterSingleton<TService, TImpl>(key);
                break;
            case ServiceLifetime.Transient:
                container.RegisterTransient<TService, TImpl>(key);
                break;
            case ServiceLifetime.Scoped:
                container.RegisterScoped<TService, TImpl>(key);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
    }

    /// <summary>
    /// Calls the self-registering TryRegister method that matches <paramref name="lifetime"/>.
    /// </summary>
    /// <typeparam name="T">The service type, which is also the implementation type.</typeparam>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime to register with.</param>
    /// <returns>The result of the TryRegister call.</returns>
    private static bool TryRegisterSelfWithLifetime<T>(ServiceContainer container, ServiceLifetime lifetime)
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => container.TryRegisterSingleton<T>(),
            ServiceLifetime.Transient => container.TryRegisterTransient<T>(),
            ServiceLifetime.Scoped => container.TryRegisterScoped<T>(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
        };
    }

    /// <summary>
    /// Calls the factory TryRegister method that matches <paramref name="lifetime"/>.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime to register with.</param>
    /// <param name="factory">The instance factory.</param>
    /// <returns>The result of the TryRegister call.</returns>
    private static bool TryRegisterFactoryWithLifetime<T>(ServiceContainer container, ServiceLifetime lifetime, ServiceInstanceFactory<T> factory)
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => container.TryRegisterSingleton(factory),
            ServiceLifetime.Transient => container.TryRegisterTransient(factory),
            ServiceLifetime.Scoped => container.TryRegisterScoped(factory),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
        };
    }

    /// <summary>
    /// Calls the typed factory TryRegister method that matches <paramref name="lifetime"/>.
    /// </summary>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime to register with.</param>
    /// <param name="factory">The instance factory.</param>
    /// <returns>The result of the TryRegister call.</returns>
    private static bool TryRegisterTypedFactoryWithLifetime(ServiceContainer container, ServiceLifetime lifetime, ServiceInstanceFactory<DependentService> factory)
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => container.TryRegisterSingleton<IDependentService, DependentService>(factory),
            ServiceLifetime.Transient => container.TryRegisterTransient<IDependentService, DependentService>(factory),
            ServiceLifetime.Scoped => container.TryRegisterScoped<IDependentService, DependentService>(factory),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
        };
    }

    /// <summary>
    /// Resolves <typeparamref name="T"/> twice from one scope, keyed when <paramref name="key"/> is not null.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="container">The container to resolve from.</param>
    /// <param name="key">The optional service key.</param>
    /// <returns>The two resolved instances.</returns>
    private static (T First, T Second) ResolveTwiceInScope<T>(ServiceContainer container, string? key)
        where T : class
    {
        using var scope = container.CreateScope();
        var provider = scope.ServiceProvider;
        return key is null
            ? (provider.GetRequiredService<T>(), provider.GetRequiredService<T>())
            : (provider.GetRequiredKeyedService<T>(key), provider.GetRequiredKeyedService<T>(key));
    }

    /// <summary>
    /// Asserts that two instances resolved in one scope are shared unless the lifetime is transient.
    /// </summary>
    /// <param name="lifetime">The lifetime the service was registered with.</param>
    /// <param name="first">The first resolved instance.</param>
    /// <param name="second">The second resolved instance.</param>
    private static void AssertSharingFor(ServiceLifetime lifetime, object first, object second)
    {
        if (lifetime == ServiceLifetime.Transient)
            Assert.NotSame(first, second);
        else
            Assert.Same(first, second);
    }

    #endregion

    #region Lifetime Theories

    [Theory]
    [InlineData(ServiceLifetime.Singleton, null)]
    [InlineData(ServiceLifetime.Transient, null)]
    [InlineData(ServiceLifetime.Scoped, null)]
    [InlineData(ServiceLifetime.Singleton, "KeyA")]
    [InlineData(ServiceLifetime.Transient, "TransientKey")]
    [InlineData(ServiceLifetime.Scoped, "ScopedKey")]
    public void TryRegister_WithNewService_ReturnsTrueAndRegisters(ServiceLifetime lifetime, string? key)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = TryRegisterWithLifetime<ITestService, TestService>(container, lifetime, key);

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<ITestService>(key));
        var (first, second) = ResolveTwiceInScope<ITestService>(container, key);
        Assert.IsType<TestService>(first);
        AssertSharingFor(lifetime, first, second);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, null)]
    [InlineData(ServiceLifetime.Transient, null)]
    [InlineData(ServiceLifetime.Scoped, null)]
    [InlineData(ServiceLifetime.Singleton, "KeyA")]
    [InlineData(ServiceLifetime.Transient, "TransientKey")]
    [InlineData(ServiceLifetime.Scoped, "ScopedKey")]
    public void TryRegister_WithExistingService_ReturnsFalseAndKeepsOriginal(ServiceLifetime lifetime, string? key)
    {
        // Arrange
        using var container = new ServiceContainer();
        RegisterWithLifetime<ITestService, TestService>(container, lifetime, key);

        // Act
        bool result = TryRegisterWithLifetime<ITestService, AlternativeTestService>(container, lifetime, key);

        // Assert
        Assert.False(result);
        var (first, second) = ResolveTwiceInScope<ITestService>(container, key);
        Assert.IsType<TestService>(first);
        AssertSharingFor(lifetime, first, second);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    public void TryRegister_SameType_WithNewService_ReturnsTrue(ServiceLifetime lifetime)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = TryRegisterSelfWithLifetime<TestService>(container, lifetime);

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<TestService>(null));
        var (first, second) = ResolveTwiceInScope<TestService>(container, key: null);
        AssertSharingFor(lifetime, first, second);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, 1, true)]
    [InlineData(ServiceLifetime.Transient, 2, false)]
    [InlineData(ServiceLifetime.Scoped, 1, true)]
    public void TryRegister_WithFactory_WithNewService_ReturnsTrueAndCallsFactoryPerLifetime(ServiceLifetime lifetime, int expectedFactoryCalls, bool sameInstance)
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;

        // Act
        bool result = TryRegisterFactoryWithLifetime<ITestService>(container, lifetime, (sp, key) =>
        {
            factoryCallCount++;
            return new TestService { Name = $"Instance{factoryCallCount}" };
        });

        // Assert
        Assert.True(result);
        var (first, second) = ResolveTwiceInScope<ITestService>(container, key: null);
        Assert.Equal("Instance1", first.Name);
        Assert.Equal(sameInstance, ReferenceEquals(first, second));
        Assert.Equal(expectedFactoryCalls, factoryCallCount);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    public void TryRegister_WithFactory_WithExistingService_ReturnsFalseAndDoesNotCallFactory(ServiceLifetime lifetime)
    {
        // Arrange
        using var container = new ServiceContainer();
        RegisterWithLifetime<ITestService, TestService>(container, lifetime, key: null);
        int factoryCallCount = 0;

        // Act
        bool result = TryRegisterFactoryWithLifetime<ITestService>(container, lifetime, (sp, key) =>
        {
            factoryCallCount++;
            return new AlternativeTestService();
        });

        // Assert
        Assert.False(result);
        Assert.Equal(0, factoryCallCount);
        var (first, _) = ResolveTwiceInScope<ITestService>(container, key: null);
        Assert.IsType<TestService>(first);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, 1, true)]
    [InlineData(ServiceLifetime.Transient, 2, false)]
    [InlineData(ServiceLifetime.Scoped, 1, true)]
    public void TryRegister_WithTypedFactory_WithNewService_ReturnsTrueAndCallsFactoryPerLifetime(ServiceLifetime lifetime, int expectedFactoryCalls, bool sameInstance)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        int factoryCallCount = 0;

        // Act
        bool result = TryRegisterTypedFactoryWithLifetime(container, lifetime, (sp, key) =>
        {
            factoryCallCount++;
            return new DependentService(sp.GetRequiredService<ITestService>());
        });

        // Assert
        Assert.True(result);
        var (first, second) = ResolveTwiceInScope<IDependentService>(container, key: null);
        Assert.Equal(sameInstance, ReferenceEquals(first, second));
        Assert.Equal(expectedFactoryCalls, factoryCallCount);
        Assert.NotNull(first.PrimaryDependency);
    }

    #endregion

    #region Singleton Instance Tests

    [Fact]
    public void TryRegisterSingleton_WithInstance_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        var instance = new TestService { Name = "PreCreatedInstance" };

        // Act
        bool result = container.TryRegisterSingleton<ITestService>(instance);

        // Assert
        Assert.True(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.Same(instance, service);
        Assert.Equal("PreCreatedInstance", service.Name);
    }

    [Fact]
    public void TryRegisterSingleton_WithInstance_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        var firstInstance = new TestService { Name = "First" };
        var secondInstance = new TestService { Name = "Second" };
        container.RegisterSingleton<ITestService>(firstInstance);

        // Act
        bool result = container.TryRegisterSingleton<ITestService>(secondInstance);

        // Assert
        Assert.False(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.Same(firstInstance, service);
        Assert.Equal("First", service.Name);
    }

    [Fact]
    public void TryRegisterSingleton_WithTypedInstance_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        var instance = new DependentService(container.GetRequiredService<ITestService>());

        // Act
        bool result = container.TryRegisterSingleton<IDependentService, DependentService>(instance);

        // Assert
        Assert.True(result);
        var service = container.GetRequiredService<IDependentService>();
        Assert.Same(instance, service);
    }

    #endregion

    #region Scope Tests

    [Fact]
    public void TryRegisterScoped_DifferentScopes_ShouldCreateDifferentInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.TryRegisterScoped<ITestService, TestService>();

        // Act & Assert
        using var scope1 = container.CreateScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<ITestService>();
        service1.Name = "Scope1";

        using var scope2 = container.CreateScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<ITestService>();
        service2.Name = "Scope2";

        Assert.NotSame(service1, service2);
        Assert.Equal("Scope1", service1.Name);
        Assert.Equal("Scope2", service2.Name);
    }

    #endregion

    #region Keyed Service Tests

    [Fact]
    public void TryRegisterSingleton_DifferentKeys_ShouldBothSucceed()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result1 = container.TryRegisterSingleton<IKeyedService, KeyedServiceA>("KeyA");
        bool result2 = container.TryRegisterSingleton<IKeyedService, KeyedServiceB>("KeyB");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        var serviceA = container.GetRequiredKeyedService<IKeyedService>("KeyA");
        var serviceB = container.GetRequiredKeyedService<IKeyedService>("KeyB");
        Assert.IsType<KeyedServiceA>(serviceA);
        Assert.IsType<KeyedServiceB>(serviceB);
    }

    #endregion

    #region Multiple Registration Tests

    [Fact]
    public void TryRegisterSingleton_MultipleCalls_FirstShouldSucceedRestShouldFail()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result1 = container.TryRegisterSingleton<ITestService, TestService>();
        bool result2 = container.TryRegisterSingleton<ITestService, AlternativeTestService>();
        bool result3 = container.TryRegisterSingleton<ITestService, TestService>();

        // Assert
        Assert.True(result1);
        Assert.False(result2);
        Assert.False(result3);
        Assert.Equal(1, container.Count);
    }

    [Fact]
    public void TryRegister_MixedLifetimes_FirstRegistrationWins()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool singletonResult = container.TryRegisterSingleton<ITestService, TestService>();
        bool transientResult = container.TryRegisterTransient<ITestService, TestService>();
        bool scopedResult = container.TryRegisterScoped<ITestService, TestService>();

        // Assert
        Assert.True(singletonResult);
        Assert.False(transientResult);
        Assert.False(scopedResult);

        // The first registration was a singleton, so both resolutions return the same instance.
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        Assert.Same(service1, service2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RegularRegister_AfterTryRegister_ShouldOverwrite()
    {
        // Arrange
        using var container = TestHelpers.CreateMutableContainer();
        container.TryRegisterSingleton<ITestService, TestService>();

        // Act
        container.RegisterSingleton<ITestService, AlternativeTestService>();

        // Assert
        var service = container.GetRequiredService<ITestService>();
        Assert.IsType<AlternativeTestService>(service);
    }

    [Fact]
    public void TryRegisterSingleton_WithNullServiceKey_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result1 = container.TryRegisterSingleton<ITestService, TestService>(serviceKey: null);
        bool result2 = container.TryRegisterSingleton<ITestService, AlternativeTestService>(serviceKey: null);

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void TryRegister_WithComplexDependencies_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result1 = container.TryRegisterSingleton<ITestService, TestService>();
        bool result2 = container.TryRegisterSingleton<IDependentService, DependentService>();
        bool result3 = container.TryRegisterSingleton<IComplexService, ComplexService>();

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);

        var complexService = container.GetRequiredService<IComplexService>();
        Assert.NotNull(complexService.TestService);
        Assert.NotNull(complexService.DependentService);
    }

    #endregion
}
