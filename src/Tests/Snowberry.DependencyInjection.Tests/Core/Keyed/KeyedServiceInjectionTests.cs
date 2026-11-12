using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Keyed;

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
        container.RegisterSingleton<IKeyedService, KeyedServiceA>("primary");
        container.RegisterSingleton<IKeyedService, KeyedServiceB>("secondary");
        container.RegisterSingleton<KeyedConstructorInjectionService>();

        // Act
        var service = container.GetRequiredService<KeyedConstructorInjectionService>();

        // Assert
        Assert.NotNull(service.PrimaryService);
        Assert.NotNull(service.SecondaryService);
        Assert.IsType<KeyedServiceA>(service.PrimaryService);
        Assert.IsType<KeyedServiceB>(service.SecondaryService);
        Assert.Equal("Primary: KeyA, Secondary: KeyB", service.GetServiceKeys());
    }

    [Fact]
    public void KeyedConstructorInjection_WithMissingKeyedService_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IKeyedService, KeyedServiceA>("primary");
        // Missing "secondary" key registration
        container.RegisterSingleton<KeyedConstructorInjectionService>();

        // Act & Assert
        Assert.Throws<ServiceTypeNotRegistered>(container.GetRequiredService<KeyedConstructorInjectionService>);
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
        Assert.NotNull(service.BaseKeyedService); // Inherited keyed property
        Assert.NotNull(service.DerivedKeyedService); // Derived keyed property
        Assert.IsType<TestService>(service.BaseKeyedService);
        Assert.IsType<DependentService>(service.DerivedKeyedService);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, true, true)]
    [InlineData(ServiceLifetime.Transient, false, false)]
    [InlineData(ServiceLifetime.Scoped, true, false)]
    public void KeyedInjection_ShouldRespectLifetimeBoundaries(ServiceLifetime lifetime, bool sameWithinScope, bool sameAcrossScopes)
    {
        // Arrange
        using var container = new ServiceContainer();
        RegisterKeyedConstructorServices(container, lifetime);

        KeyedConstructorInjectionService? withinScope1 = null, withinScope2 = null, otherScope = null;

        // Act
        TestHelpers.ForEachScope(container, 2, (scope, index) =>
        {
            if (index == 0)
            {
                withinScope1 = scope.ServiceProvider.GetRequiredService<KeyedConstructorInjectionService>();
                withinScope2 = scope.ServiceProvider.GetRequiredService<KeyedConstructorInjectionService>();
            }
            else
            {
                otherScope = scope.ServiceProvider.GetRequiredService<KeyedConstructorInjectionService>();
            }
        });

        // Assert
        Assert.Equal(sameWithinScope, ReferenceEquals(withinScope1, withinScope2));
        Assert.Equal(sameWithinScope, ReferenceEquals(withinScope1!.PrimaryService, withinScope2!.PrimaryService));
        Assert.Equal(sameWithinScope, ReferenceEquals(withinScope1.SecondaryService, withinScope2.SecondaryService));
        Assert.Equal(sameAcrossScopes, ReferenceEquals(withinScope1, otherScope));
        Assert.Equal(sameAcrossScopes, ReferenceEquals(withinScope1.PrimaryService, otherScope!.PrimaryService));
        Assert.Equal(sameAcrossScopes, ReferenceEquals(withinScope1.SecondaryService, otherScope.SecondaryService));
    }

    /// <summary>
    /// Registers the two keyed services and their consumer with the same lifetime.
    /// </summary>
    /// <param name="container">The container to register in.</param>
    /// <param name="lifetime">The lifetime for every registration.</param>
    private static void RegisterKeyedConstructorServices(ServiceContainer container, ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                container.RegisterSingleton<IKeyedService, KeyedServiceA>("primary");
                container.RegisterSingleton<IKeyedService, KeyedServiceB>("secondary");
                container.RegisterSingleton<KeyedConstructorInjectionService>();
                break;
            case ServiceLifetime.Transient:
                container.RegisterTransient<IKeyedService, KeyedServiceA>("primary");
                container.RegisterTransient<IKeyedService, KeyedServiceB>("secondary");
                container.RegisterTransient<KeyedConstructorInjectionService>();
                break;
            case ServiceLifetime.Scoped:
                container.RegisterScoped<IKeyedService, KeyedServiceA>("primary");
                container.RegisterScoped<IKeyedService, KeyedServiceB>("secondary");
                container.RegisterScoped<KeyedConstructorInjectionService>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
    }
}
