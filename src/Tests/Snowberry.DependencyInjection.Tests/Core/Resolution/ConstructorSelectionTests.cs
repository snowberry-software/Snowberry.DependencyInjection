using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Tests for constructor selection and instantiation functionality including
/// preferred constructor selection, parameter matching, and attribute-based selection.
/// </summary>
public class ConstructorSelectionTests
{
    /// <summary>
    /// Services with the parameter types and preferred-attribute state of the constructor the container must select.
    /// </summary>
    public static TheoryData<string, Type, Type[], bool> SelectedConstructors => new()
    {
        { "single constructor without parameters", typeof(ParameterlessService), Type.EmptyTypes, false },
        { "single constructor with one parameter", typeof(SingleParameterService), [typeof(string)], false },
        { "single constructor with three parameters", typeof(MultiParameterService), [typeof(string), typeof(int), typeof(bool)], false },
        { "preferred attribute wins over fewer parameters", typeof(PreferredConstructorService), [typeof(string), typeof(int)], true },
        { "most parameters without preferred attribute", typeof(ComplexConstructorService), [typeof(ITestService), typeof(IDependentService)], false },
    };

    [Theory]
    [MemberData(nameof(SelectedConstructors))]
    public void GetConstructor_ShouldSelectExpectedConstructor(string caseName, Type serviceType, Type[] parameterTypes, bool hasPreferredAttribute)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(serviceType);

        // Assert
        Assert.NotNull(constructor);
        Assert.Equal(serviceType, constructor.DeclaringType);
        Assert.Equal(parameterTypes, constructor.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.True(hasPreferredAttribute == (constructor.GetCustomAttribute<PreferredConstructorAttribute>() != null), caseName);
    }

    [Fact]
    public void ConstructorSelection_WithDependencyInjection_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<DependentService>();

        // Act
        var service = container.GetRequiredService<DependentService>();

        // Assert
        Assert.IsType<TestService>(service.PrimaryDependency);
    }

    [Fact]
    public void ConstructorSelection_WithAllDependenciesRegistered_ShouldSelectMostComplex()
    {
        // Tests that when all dependencies are registered, the constructor with most parameters is used

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<ComplexConstructorService>();

        // Act
        var service = container.GetRequiredService<ComplexConstructorService>();

        // Assert
        Assert.NotNull(service.TestService);
        Assert.NotNull(service.DependentService);
        Assert.Equal(2, service.ConstructorUsed); // Should use the constructor with 2 parameters
    }

    [Fact]
    public void ConstructorSelection_WithPreferredAttribute_ShouldAlwaysSelectPreferred()
    {
        // Tests that PreferredConstructor attribute overrides parameter count preference

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<PreferredConstructorComplexService>();

        // Act
        var service = container.GetRequiredService<PreferredConstructorComplexService>();

        // Assert
        Assert.NotNull(service.TestService);
        Assert.NotNull(service.DependentService);
        Assert.Equal(2, service.ConstructorUsed); // Should use the preferred constructor

        // Verify the constructor has the PreferredConstructor attribute
        var constructor = container.ServiceFactory.GetConstructor(typeof(PreferredConstructorComplexService));
        Assert.NotNull(constructor?.GetCustomAttribute<PreferredConstructorAttribute>());
    }

    [Fact]
    public void ConstructorSelection_WithMissingDependencies_ShouldThrowException()
    {
        // Tests that missing dependencies cause an exception during CreateInstance
        // GetConstructor still selects the constructor with most parameters regardless of dependency availability

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        // Don't register IDependentService - this should cause an exception
        container.RegisterSingleton<ComplexConstructorService>();

        // Act & Assert
        // The constructor selection will still pick the one with most parameters,
        // but CreateInstance will fail due to missing dependency
        var constructor = container.ServiceFactory.GetConstructor(typeof(ComplexConstructorService));
        Assert.NotNull(constructor);
        Assert.Equal(2, constructor.GetParameters().Length); // Still selects constructor with most parameters

        // But service resolution should fail due to missing IDependentService
        Assert.ThrowsAny<Exception>(container.GetRequiredService<ComplexConstructorService>);
    }

    [Fact]
    public void ConstructorSelection_WithUnavailablePreferredDependencies_ShouldThrowException()
    {
        // Tests that unavailable dependencies for preferred constructor cause an exception

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        // Don't register IComplexService, so preferred constructor can't be satisfied
        container.RegisterSingleton<FallbackConstructorService>();

        // Act & Assert
        // GetConstructor should still select the preferred constructor
        var constructor = container.ServiceFactory.GetConstructor(typeof(FallbackConstructorService));
        Assert.NotNull(constructor);
        Assert.NotNull(constructor.GetCustomAttribute<PreferredConstructorAttribute>());
        Assert.Equal(2, constructor.GetParameters().Length);

        // But service resolution should fail due to missing IComplexService
        Assert.ThrowsAny<Exception>(container.GetRequiredService<FallbackConstructorService>);
    }

    [Fact]
    public void ConstructorSelection_WithNoRegisteredDependencies_ShouldThrowException()
    {
        // Tests behavior when no dependencies are registered but constructor needs them

        // Arrange
        using var container = new ServiceContainer();
        // Don't register any dependencies
        container.RegisterSingleton<ComplexConstructorService>();

        // Act & Assert
        // GetConstructor should still select the constructor with most parameters
        var constructor = container.ServiceFactory.GetConstructor(typeof(ComplexConstructorService));
        Assert.NotNull(constructor);
        Assert.Equal(2, constructor.GetParameters().Length);

        // But service resolution should fail due to missing dependencies
        Assert.ThrowsAny<Exception>(container.GetRequiredService<ComplexConstructorService>);
    }

    [Fact]
    public void ConstructorSelection_WithParameterlessOnly_ShouldWork()
    {
        // Tests that services with only parameterless constructors work correctly

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ParameterlessService>();

        // Act
        var service = container.GetRequiredService<ParameterlessService>();

        // Assert
        Assert.NotEmpty(service.ServiceId);
    }
}
