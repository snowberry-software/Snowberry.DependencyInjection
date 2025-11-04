using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for TryRegister extension methods including TryRegisterSingleton, TryRegisterTransient, and TryRegisterScoped.
/// Verifies that these methods correctly return true when registration succeeds and false when service already exists.
/// </summary>
public class TryRegisterMethodsTests
{
    #region TryRegisterSingleton Tests

    [Fact]
    public void TryRegisterSingleton_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterSingleton<ITestService, TestService>();

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<ITestService>(null));
        var service = container.GetRequiredService<ITestService>();
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void TryRegisterSingleton_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterSingleton<ITestService, AlternativeTestService>();

        // Assert
        Assert.False(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void TryRegisterSingleton_SameType_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterSingleton<TestService>();

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<TestService>(null));
        var service = container.GetRequiredService<TestService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void TryRegisterSingleton_WithFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterSingleton<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new TestService { Name = "FactoryCreated" };
        });

        // Assert
        Assert.True(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.Equal("FactoryCreated", service.Name);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void TryRegisterSingleton_WithFactory_WithExistingService_ShouldReturnFalseAndNotCallFactory()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterSingleton<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new AlternativeTestService();
        });

        // Assert
        Assert.False(result);
        Assert.Equal(0, factoryCallCount);
        var service = container.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(service);
    }

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
    public void TryRegisterSingleton_WithTypedFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterSingleton<IDependentService, DependentService>(
            (sp, key) => new DependentService(sp.GetRequiredService<ITestService>()));

        // Assert
        Assert.True(result);
        var service = container.GetRequiredService<IDependentService>();
        Assert.NotNull(service);
        Assert.NotNull(service.PrimaryDependency);
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

    #region TryRegisterTransient Tests

    [Fact]
    public void TryRegisterTransient_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterTransient<ITestService, TestService>();

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<ITestService>(null));
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2); // Transient should create new instances
    }

    [Fact]
    public void TryRegisterTransient_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterTransient<ITestService, AlternativeTestService>();

        // Assert
        Assert.False(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void TryRegisterTransient_SameType_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterTransient<TestService>();

        // Assert
        Assert.True(result);
        var service1 = container.GetRequiredService<TestService>();
        var service2 = container.GetRequiredService<TestService>();
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void TryRegisterTransient_WithFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterTransient<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new TestService { Name = $"Instance{factoryCallCount}" };
        });

        // Assert
        Assert.True(result);
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        Assert.Equal("Instance1", service1.Name);
        Assert.Equal("Instance2", service2.Name);
        Assert.Equal(2, factoryCallCount);
    }

    [Fact]
    public void TryRegisterTransient_WithFactory_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterTransient<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new AlternativeTestService();
        });

        // Assert
        Assert.False(result);
        Assert.Equal(0, factoryCallCount);
    }

    [Fact]
    public void TryRegisterTransient_WithTypedFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterTransient<IDependentService, DependentService>(
            instanceFactory: (sp, key) =>
            {
                factoryCallCount++;
                return new DependentService(sp.GetRequiredService<ITestService>());
            });

        // Assert
        Assert.True(result);
        var service1 = container.GetRequiredService<IDependentService>();
        var service2 = container.GetRequiredService<IDependentService>();
        Assert.NotSame(service1, service2);
        Assert.Equal(2, factoryCallCount);
    }

    #endregion

    #region TryRegisterScoped Tests

    [Fact]
    public void TryRegisterScoped_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterScoped<ITestService, TestService>();

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<ITestService>(null));

        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<ITestService>();
        Assert.Same(service1, service2); // Scoped should reuse instance within scope
    }

    [Fact]
    public void TryRegisterScoped_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterScoped<ITestService, AlternativeTestService>();

        // Assert
        Assert.False(result);
        using var scope = container.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void TryRegisterScoped_SameType_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        bool result = container.TryRegisterScoped<TestService>();

        // Assert
        Assert.True(result);
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<TestService>();
        Assert.Same(service1, service2);
    }

    [Fact]
    public void TryRegisterScoped_WithFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterScoped<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new TestService { Name = $"ScopedInstance{factoryCallCount}" };
        });

        // Assert
        Assert.True(result);
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var service2 = scope.ServiceProvider.GetRequiredService<ITestService>();
        Assert.Same(service1, service2);
        Assert.Equal("ScopedInstance1", service1.Name);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void TryRegisterScoped_WithFactory_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        int factoryCallCount = 0;

        // Act
        bool result = container.TryRegisterScoped<ITestService>((sp, key) =>
        {
            factoryCallCount++;
            return new AlternativeTestService();
        });

        // Assert
        Assert.False(result);
        Assert.Equal(0, factoryCallCount);
    }

    [Fact]
    public void TryRegisterScoped_WithTypedFactory_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterScoped<IDependentService, DependentService>(
            (sp, key) => new DependentService(sp.GetRequiredService<ITestService>()));

        // Assert
        Assert.True(result);
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredService<IDependentService>();
        var service2 = scope.ServiceProvider.GetRequiredService<IDependentService>();
        Assert.Same(service1, service2);
    }

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
    public void TryRegisterSingleton_WithServiceKey_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "KeyA";

        // Act
        bool result = container.TryRegisterSingleton<IKeyedService, KeyedServiceA>(serviceKey);

        // Assert
        Assert.True(result);
        Assert.True(container.IsServiceRegistered<IKeyedService>(serviceKey));
        var service = container.GetRequiredKeyedService<IKeyedService>(serviceKey);
        Assert.IsType<KeyedServiceA>(service);
    }

    [Fact]
    public void TryRegisterSingleton_WithServiceKey_WithExistingService_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "KeyA";
        container.RegisterSingleton<IKeyedService, KeyedServiceA>(serviceKey);

        // Act
        bool result = container.TryRegisterSingleton<IKeyedService, KeyedServiceB>(serviceKey);

        // Assert
        Assert.False(result);
        var service = container.GetRequiredKeyedService<IKeyedService>(serviceKey);
        Assert.IsType<KeyedServiceA>(service);
    }

    [Fact]
    public void TryRegisterTransient_WithServiceKey_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "TransientKey";

        // Act
        bool result = container.TryRegisterTransient<ITestService, TestService>(serviceKey);

        // Assert
        Assert.True(result);
        var service1 = container.GetRequiredKeyedService<ITestService>(serviceKey);
        var service2 = container.GetRequiredKeyedService<ITestService>(serviceKey);
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void TryRegisterScoped_WithServiceKey_WithNewService_ShouldReturnTrue()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string serviceKey = "ScopedKey";

        // Act
        bool result = container.TryRegisterScoped<ITestService, TestService>(serviceKey);

        // Assert
        Assert.True(result);
        using var scope = container.CreateScope();
        var service1 = scope.ServiceProvider.GetRequiredKeyedService<ITestService>(serviceKey);
        var service2 = scope.ServiceProvider.GetRequiredKeyedService<ITestService>(serviceKey);
        Assert.Same(service1, service2);
    }

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

        // Verify it's actually a singleton
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        Assert.Same(service1, service2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TryRegisterSingleton_AfterRegularRegistration_ShouldReturnFalse()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        // Act
        bool result = container.TryRegisterSingleton<ITestService, AlternativeTestService>();

        // Assert
        Assert.False(result);
        var service = container.GetRequiredService<ITestService>();
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void RegularRegister_AfterTryRegister_ShouldOverwrite()
    {
        // Arrange
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
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
