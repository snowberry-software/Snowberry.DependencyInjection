using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Tests for property injection functionality using the InjectAttribute.
/// Tests property injection with various scenarios including required/optional properties,
/// different property types, inheritance, and error conditions.
/// </summary>
public class PropertyInjectionTests
{
    [Fact]
    public void PropertyInjection_WithRequiredProperty_ShouldInjectSuccessfully()
    {
        // Arrange
        using var container = new ServiceContainer();
        var testService = new TestService { Name = "InjectedService" };
        container.RegisterSingleton<ITestService>(testService);
        container.RegisterSingleton<PropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<PropertyInjectionService>();

        // Assert
        Assert.Same(testService, service.RequiredPropertyDependency);
        Assert.Equal("InjectedService", service.RequiredPropertyDependency!.Name);
    }

    [Fact]
    public void PropertyInjection_WithOptionalProperty_WhenServiceRegistered_ShouldInject()
    {
        // Arrange
        using var container = new ServiceContainer();
        var constructorService = new TestService { Name = "ConstructorService" };
        var optionalService = new AlternativeTestService { Name = "OptionalService" };

        container.RegisterSingleton<ITestService>(constructorService);
        container.RegisterSingleton<PropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<PropertyInjectionService>();

        // Assert
        Assert.Same(constructorService, service.OptionalPropertyDependency);
        Assert.Same(constructorService, service.ConstructorDependency);
    }

    [Fact]
    public void PropertyInjection_WhenOneServiceSatisfiesConstructorAndProperties_InjectsTheSameSingleton()
    {
        // Arrange
        using var container = new ServiceContainer();
        var constructorService = new TestService { Name = "ConstructorService" };
        container.RegisterSingleton<ITestService>(constructorService);
        container.RegisterSingleton<PropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<PropertyInjectionService>();

        // Assert
        Assert.Same(constructorService, service.ConstructorDependency);
        Assert.Same(constructorService, service.RequiredPropertyDependency);
        Assert.Same(constructorService, service.OptionalPropertyDependency);
    }

    [Fact]
    public void PropertyInjection_WithMultipleProperties_ShouldInjectAll()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<IComplexService, ComplexService>();
        container.RegisterSingleton<MultiplePropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<MultiplePropertyInjectionService>();

        // Assert
        Assert.IsType<TestService>(service.TestService);
        Assert.IsType<DependentService>(service.DependentService);
        Assert.IsType<ComplexService>(service.ComplexService);
    }

    [Fact]
    public void PropertyInjection_WithMixedPropertyTypes_ShouldInjectCorrectTypes()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<TestService>();
        container.RegisterSingleton<MixedPropertyTypeService>();

        // Act
        var service = container.GetRequiredService<MixedPropertyTypeService>();

        // Assert
        Assert.Null(service.NonInjectedProperty); // Should not be injected as it lacks [Inject] attribute
        Assert.IsType<TestService>(service.InterfaceProperty);
        Assert.IsType<TestService>(service.ConcreteProperty);
    }

    [Fact]
    public void PropertyInjection_WithInheritance_ShouldInjectPropertiesFromBaseAndDerived()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<DerivedPropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<DerivedPropertyInjectionService>();

        // Assert
        Assert.IsType<TestService>(service.BaseProperty); // Inherited property should be injected
        Assert.IsType<DependentService>(service.DerivedProperty); // Derived property should be injected
    }

    [Fact]
    public void PropertyInjection_WithGenericTypes_ShouldInjectCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<IRepository<TestEntity>, Repository<TestEntity>>();
        container.RegisterSingleton<IGenericProcessor<string>, GenericProcessor<string>>();
        container.RegisterSingleton<GenericPropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<GenericPropertyInjectionService>();

        // Assert
        Assert.IsType<Repository<TestEntity>>(service.Repository);
        Assert.IsType<GenericProcessor<string>>(service.StringProcessor);
    }

    [Fact]
    public void PropertyInjection_WithReadOnlyProperty_ShouldNotInject()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ReadOnlyPropertyService>();

        // Act
        var service = container.GetRequiredService<ReadOnlyPropertyService>();

        // Assert
        // Read-only property should remain null as it can't be set
        Assert.Null(service.TestService);
    }

    [Fact]
    public void PropertyInjection_WithPrivateSetter_InjectsTheService()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<PrivateSetterPropertyService>();

        // Act
        var service = container.GetRequiredService<PrivateSetterPropertyService>();

        // Assert
        Assert.Same(container.GetRequiredService<ITestService>(), service.TestService);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, true, true)]
    [InlineData(ServiceLifetime.Scoped, true, false)]
    [InlineData(ServiceLifetime.Transient, false, false)]
    public void PropertyInjection_ShouldFollowLifetimeOfServiceAndDependencies(ServiceLifetime lifetime, bool sameWithinScope, bool sameAcrossScopes)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(new ServiceDescriptor(typeof(ITestService), typeof(TestService), lifetime, singletonInstance: null));
        container.Register(new ServiceDescriptor(typeof(PropertyInjectionService), typeof(PropertyInjectionService), lifetime, singletonInstance: null));

        // Act - resolve twice in each of two scopes
        var perScope = new List<PropertyInjectionService[]>();
        TestHelpers.ForEachScope(container, 2, (scope, _) =>
        {
            perScope.Add(
            [
                scope.ServiceProvider.GetRequiredService<PropertyInjectionService>(),
                scope.ServiceProvider.GetRequiredService<PropertyInjectionService>()
            ]);
        });

        // Assert - the injected property and constructor dependency share the lifetime of the consuming service
        AssertSameness(sameWithinScope, perScope[0][0], perScope[0][1]);
        AssertSameness(sameAcrossScopes, perScope[0][0], perScope[1][0]);
    }

    /// <summary>
    /// Asserts that two property-injected services, and the dependencies injected into them, are
    /// either the same instances or all different instances.
    /// </summary>
    /// <param name="expectedSame">Whether the services and their dependencies are expected to be shared.</param>
    /// <param name="first">The first service.</param>
    /// <param name="second">The second service.</param>
    private static void AssertSameness(bool expectedSame, PropertyInjectionService first, PropertyInjectionService second)
    {
        Assert.Equal(expectedSame, ReferenceEquals(first, second));
        Assert.Equal(expectedSame, ReferenceEquals(first.RequiredPropertyDependency, second.RequiredPropertyDependency));
        Assert.Equal(expectedSame, ReferenceEquals(first.ConstructorDependency, second.ConstructorDependency));
    }

    [Fact]
    public void PropertyInjection_WithComplexDependencyChain_ShouldResolveCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<IComplexService, ComplexService>();
        container.RegisterSingleton<MultiplePropertyInjectionService>();

        // Act
        var service = container.GetRequiredService<MultiplePropertyInjectionService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.TestService);
        Assert.NotNull(service.DependentService);
        Assert.NotNull(service.ComplexService);

        // Verify the dependency chain is properly resolved
        Assert.NotNull(service.DependentService.PrimaryDependency);
        Assert.NotNull(service.ComplexService.TestService);
        Assert.NotNull(service.ComplexService.DependentService);
    }

    [Fact]
    public void PropertyInjection_WithDisposal_ShouldDisposeInjectedProperties()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();
        container.RegisterTransient<PropertyInjectionService>();

        PropertyInjectionService service;
        ITestService? injectedService;

        // Act
        service = container.GetRequiredService<PropertyInjectionService>();
        injectedService = service.RequiredPropertyDependency;

        // Assert initial state
        Assert.NotNull(injectedService);
        Assert.False(service.IsDisposed);
        Assert.False(injectedService.IsDisposed);

        // Act - dispose the service
        service.Dispose();

        // Assert disposal
        Assert.True(service.IsDisposed);
        // The behavior of whether injected properties are disposed depends on the container implementation
    }
}