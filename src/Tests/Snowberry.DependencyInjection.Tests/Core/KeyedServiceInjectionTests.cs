using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for keyed service injection functionality using the FromKeyedServicesAttribute.
/// Tests keyed service injection in constructor parameters and properties with various scenarios
/// including different key types, mixed keyed/non-keyed injection, and error conditions.
/// </summary>
public class KeyedServiceInjectionTests
{
    [Fact]
    public void KeyedConstructorInjection_WithRegisteredServices_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, PrimaryKeyedService>("primary");
        container.RegisterSingleton<IKeyedService, SecondaryKeyedService>("secondary");
        container.RegisterSingleton<KeyedConstructorInjectionService>();

        // Act
        var service = container.GetRequiredService<KeyedConstructorInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.PrimaryService);
        Assert.NotNull(service.SecondaryService);
        Assert.IsType<PrimaryKeyedService>(service.PrimaryService);
        Assert.IsType<SecondaryKeyedService>(service.SecondaryService);
        Assert.Equal("Primary: Primary, Secondary: Secondary", service.GetServiceKeys());
    }

    [Fact]
    public void KeyedConstructorInjection_WithMissingKeyedService_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, PrimaryKeyedService>("primary");
        // Missing "secondary" key registration
        container.RegisterSingleton<KeyedConstructorInjectionService>();

        // Act & Assert
        Assert.Throws<ServiceTypeNotRegistered>(() =>
            container.GetRequiredService<KeyedConstructorInjectionService>());
    }

    [Fact]
    public void KeyedPropertyInjection_WithRegisteredServices_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ITestService, TestService>("keyed-test");
        container.RegisterSingleton<IDependentService, DependentService>("keyed-dependent");
        container.RegisterSingleton<KeyedPropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<KeyedPropertyInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.KeyedTestService);
        Assert.NotNull(service.KeyedDependentService);
        Assert.IsType<TestService>(service.KeyedTestService);
        Assert.IsType<DependentService>(service.KeyedDependentService);
    }

    [Fact]
    public void KeyedPropertyInjection_WithOptionalMissingService_ShouldLeaveNull()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ITestService, TestService>("keyed-test");
        container.RegisterSingleton<IDependentService, DependentService>("keyed-dependent");
        // Missing "optional-keyed" registration
        container.RegisterSingleton<KeyedPropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<KeyedPropertyInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.KeyedTestService);
        Assert.NotNull(service.KeyedDependentService);
        Assert.Null(service.OptionalKeyedService); // Optional service should remain null
        Assert.NotSame(service.KeyedTestService, service.KeyedDependentService.PrimaryDependency);
    }

    [Fact]
    public void MixedKeyedInjection_WithKeyedAndNonKeyedServices_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>(); // Default registration
        container.RegisterSingleton<ITestService, AlternativeTestService>("constructor-key");
        container.RegisterSingleton<IDependentService, DependentService>(); // Default registration
        container.RegisterSingleton<IDependentService, DependentService>("mixed-key");
        container.RegisterSingleton<MixedKeyedInjectionService>();

        // Act
        var service = container.GetRequiredService<MixedKeyedInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.DefaultService);
        Assert.NotNull(service.KeyedService);
        Assert.NotNull(service.DefaultPropertyService);
        Assert.NotNull(service.KeyedPropertyService);
        Assert.IsType<TestService>(service.DefaultService);
        Assert.IsType<AlternativeTestService>(service.KeyedService);
        Assert.NotSame(service.DefaultService, service.KeyedService);
    }

    [Fact]
    public void NullKeyedInjection_ShouldBehaveLikeDefaultRegistration()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>(new TestService(), serviceKey: null); // Explicit null key
        container.RegisterSingleton<IDependentService>(new DependentService(container.GetRequiredService<ITestService>()), serviceKey: null);
        container.RegisterSingleton<NullKeyedInjectionService>();

        // Act
        var service = container.GetRequiredService<NullKeyedInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.NullKeyedService);
        Assert.NotNull(service.NullKeyedProperty);
        Assert.IsType<TestService>(service.NullKeyedService);
        Assert.IsType<DependentService>(service.NullKeyedProperty);
    }

    [Fact]
    public void VariousKeyTypeInjection_WithDifferentKeyTypes_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "StringKey" }, "string-key");
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "IntKey" }, 42);
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "BoolKey" }, true);
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "DoubleKey" }, 3.14);
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "TypeKey" }, typeof(TestService));
        container.RegisterSingleton<VariousKeyTypeInjectionService>();

        // Act
        var service = container.GetRequiredService<VariousKeyTypeInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.StringKeyedService);
        Assert.NotNull(service.IntKeyedService);
        Assert.NotNull(service.BoolKeyedService);
        Assert.NotNull(service.DoubleKeyedProperty);
        Assert.NotNull(service.TypeKeyedProperty);

        Assert.Equal("StringKey", service.StringKeyedService.Name);
        Assert.Equal("IntKey", service.IntKeyedService.Name);
        Assert.Equal("BoolKey", service.BoolKeyedService.Name);
        Assert.Equal("DoubleKey", service.DoubleKeyedProperty!.Name);
        Assert.Equal("TypeKey", service.TypeKeyedProperty!.Name);
    }

    [Fact]
    public void KeyedGenericInjection_WithGenericServices_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IRepository<TestEntity>, Repository<TestEntity>>("generic-repo");
        container.RegisterSingleton<IGenericProcessor<string>, GenericProcessor<string>>("generic-processor");
        container.RegisterSingleton<KeyedGenericInjectionService>();

        // Act
        var service = container.GetRequiredService<KeyedGenericInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.KeyedRepository);
        Assert.NotNull(service.KeyedProcessor);
        Assert.IsType<Repository<TestEntity>>(service.KeyedRepository);
        Assert.IsType<GenericProcessor<string>>(service.KeyedProcessor);
    }

    [Fact]
    public void ComplexKeyedDependency_WithDependencyChains_ShouldResolveCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "BaseService" }, "base-service");
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "ChainService" }, "chain-service");
        container.RegisterSingleton<IDependentService>((provider, key) => new DependentService(
            container.GetRequiredKeyedService<ITestService>("chain-service")), "chain-start");
        container.RegisterSingleton<IComplexService>((provider, key) => new ComplexService(
            container.GetRequiredKeyedService<ITestService>("chain-service"),
            container.GetRequiredKeyedService<IDependentService>("chain-start")), "chain-end");
        container.RegisterSingleton<ComplexKeyedDependencyService>();

        // Act
        var service = container.GetRequiredService<ComplexKeyedDependencyService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.BaseService);
        Assert.NotNull(service.ChainStartService);
        Assert.NotNull(service.ChainEndService);
        Assert.Equal("BaseService", service.BaseService.Name);
    }

    [Fact]
    public void KeyedInjectionInheritance_ShouldInjectPropertiesFromBaseAndDerived()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ITestService, TestService>("base-keyed");
        container.RegisterSingleton<IDependentService, DependentService>("derived-keyed");
        container.RegisterSingleton<DerivedKeyedInjectionService>();

        // Act
        var service = container.GetRequiredService<DerivedKeyedInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.BaseKeyedService); // Inherited keyed property
        Assert.NotNull(service.DerivedKeyedService); // Derived keyed property
        Assert.IsType<TestService>(service.BaseKeyedService);
        Assert.IsType<DependentService>(service.DerivedKeyedService);
    }

    [Fact]
    public void KeyedInjection_WithSingletonLifetime_ShouldInjectSameInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, PrimaryKeyedService>("primary");
        container.RegisterSingleton<IKeyedService, SecondaryKeyedService>("secondary");
        container.RegisterSingleton<KeyedConstructorInjectionService>();

        // Act
        var service1 = container.GetRequiredService<KeyedConstructorInjectionService>();
        var service2 = container.GetRequiredService<KeyedConstructorInjectionService>();

        // Assert
        Assert.Same(service1, service2);
        Assert.Same(service1.PrimaryService, service2.PrimaryService);
        Assert.Same(service1.SecondaryService, service2.SecondaryService);
    }

    [Fact]
    public void KeyedInjection_WithTransientLifetime_ShouldCreateNewInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<IKeyedService, PrimaryKeyedService>("primary");
        container.RegisterTransient<IKeyedService, SecondaryKeyedService>("secondary");
        container.RegisterTransient<KeyedConstructorInjectionService>();

        // Act
        var service1 = container.GetRequiredService<KeyedConstructorInjectionService>();
        var service2 = container.GetRequiredService<KeyedConstructorInjectionService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.NotSame(service1.PrimaryService, service2.PrimaryService);
        Assert.NotSame(service1.SecondaryService, service2.SecondaryService);
    }

    [Fact]
    public void KeyedInjection_WithScopedLifetime_ShouldRespectScopeBoundaries()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<IKeyedService, PrimaryKeyedService>("primary");
        container.RegisterScoped<IKeyedService, SecondaryKeyedService>("secondary");
        container.RegisterScoped<KeyedConstructorInjectionService>();

        KeyedConstructorInjectionService scopedService1, scopedService2;
        KeyedConstructorInjectionService separateScopeService;

        // Act
        using (var scope1 = container.CreateScope())
        {
            scopedService1 = scope1.ServiceFactory.GetRequiredService<KeyedConstructorInjectionService>();
            scopedService2 = scope1.ServiceFactory.GetRequiredService<KeyedConstructorInjectionService>();
        }

        using (var scope2 = container.CreateScope())
        {
            separateScopeService = scope2.ServiceFactory.GetRequiredService<KeyedConstructorInjectionService>();
        }

        // Assert
        Assert.Same(scopedService1, scopedService2); // Same within scope
        Assert.NotSame(scopedService1, separateScopeService); // Different across scopes
        Assert.Same(scopedService1.PrimaryService, scopedService2.PrimaryService);
        Assert.NotSame(scopedService1.PrimaryService, separateScopeService.PrimaryService);
    }

    [Fact]
    public void KeyedInjection_WithFactoryRegistration_ShouldPassCorrectKey()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string expectedKey = "factory-key";
        object? capturedKey = null;

        container.RegisterSingleton<ITestService>((provider, key) =>
        {
            capturedKey = key;
            return new TestService { Name = key?.ToString() ?? "NoKey" };
        }, expectedKey);

        container.RegisterSingleton<NullKeyedInjectionService>();

        // Change the service to use the factory key
        var service = new NullKeyedInjectionService(
            container.GetRequiredKeyedService<ITestService>(expectedKey));

        // Assert
        Assert.Equal(expectedKey, capturedKey);
        Assert.Equal(expectedKey, service.NullKeyedService.Name);
    }

    [Fact]
    public void KeyedInjection_WithWrongKeyType_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, PrimaryKeyedService>("string-key");
        // Service expects integer key but only string key is registered

        // Act & Assert
        Assert.Throws<ServiceTypeNotRegistered>(() =>
            container.GetRequiredKeyedService<IKeyedService>(42));
    }

    [Fact]
    public void KeyedInjection_WithSameKeyDifferentServices_ShouldIsolateCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        const string sharedKey = "shared";

        container.RegisterSingleton<ITestService, TestService>(sharedKey);
        container.RegisterSingleton<IKeyedService, PrimaryKeyedService>(sharedKey);

        // Act
        var testService = container.GetRequiredKeyedService<ITestService>(sharedKey);
        var keyedService = container.GetRequiredKeyedService<IKeyedService>(sharedKey);

        // Assert
        Assert.NotNull(testService);
        Assert.NotNull(keyedService);
        Assert.IsType<TestService>(testService);
        Assert.IsType<PrimaryKeyedService>(keyedService);
    }

    [Fact]
    public void KeyedInjection_WithComplexKeyObjects_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        var complexKey = new { Name = "ComplexKey", Value = 42 };
        var enumKey = ServiceLifetime.Singleton;

        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "ComplexKeyService" }, complexKey);
        container.RegisterSingleton<ITestService>((provider, key) => new TestService { Name = "EnumKeyService" }, enumKey);

        // Act
        var complexKeyService = container.GetRequiredKeyedService<ITestService>(complexKey);
        var enumKeyService = container.GetRequiredKeyedService<ITestService>(enumKey);

        // Assert
        Assert.NotNull(complexKeyService);
        Assert.NotNull(enumKeyService);
        Assert.Equal("ComplexKeyService", complexKeyService.Name);
        Assert.Equal("EnumKeyService", enumKeyService.Name);
    }
}