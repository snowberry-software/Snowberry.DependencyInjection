using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for constructor selection and instantiation functionality including
/// preferred constructor selection, parameter matching, and attribute-based selection.
/// </summary>
public class ConstructorSelectionTests
{
    [Fact]
    public void GetConstructor_WithParameterlessService_ShouldSelectCorrect()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(typeof(ParameterlessService));

        // Assert
        Assert.NotNull(constructor);
        Assert.Empty(constructor!.GetParameters());
    }

    [Fact]
    public void GetConstructor_WithSingleParameterService_ShouldSelectCorrect()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(typeof(SingleParameterService));

        // Assert
        Assert.NotNull(constructor);
        Assert.Single(constructor!.GetParameters());
        Assert.Equal(typeof(string), constructor.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void GetConstructor_WithMultipleParameterService_ShouldSelectCorrect()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(typeof(MultiParameterService));

        // Assert
        Assert.NotNull(constructor);
        Assert.Equal(3, constructor!.GetParameters().Length);

        var parameters = constructor.GetParameters();
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal(typeof(bool), parameters[2].ParameterType);
    }

    [Fact]
    public void GetConstructor_WithPreferredConstructorAttribute_ShouldSelectPreferred()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(typeof(PreferredConstructorService));

        // Assert
        Assert.NotNull(constructor);
        Assert.Equal(2, constructor!.GetParameters().Length); // Should select the preferred one with 2 parameters
        Assert.NotNull(constructor.GetCustomAttribute<PreferredConstructorAttribute>());
    }

    [Fact]
    public void GetConstructor_WithComplexService_ShouldSelectMostParameters()
    {
        // Tests that GetConstructor selects the constructor with the most parameters
        // when no PreferredConstructor attribute is present

        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(typeof(ComplexConstructorService));

        // Assert
        Assert.NotNull(constructor);
        Assert.Equal(2, constructor!.GetParameters().Length); // Should select constructor with most parameters
        Assert.Null(constructor.GetCustomAttribute<PreferredConstructorAttribute>());
    }

    [Fact]
    public void ConstructorSelection_WithDependencyInjection_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<ServiceWithDependencies>();

        // Act
        var service = container.GetRequiredService<ServiceWithDependencies>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.TestService);
        Assert.IsType<TestService>(service.TestService);
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
        Assert.NotNull(service);
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
        Assert.NotNull(service);
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
        Assert.Equal(2, constructor!.GetParameters().Length); // Still selects constructor with most parameters

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
        Assert.NotNull(constructor!.GetCustomAttribute<PreferredConstructorAttribute>());
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
        Assert.Equal(2, constructor!.GetParameters().Length);

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
        Assert.NotNull(service);
        Assert.NotEmpty(service.ServiceId);
    }

    [Fact]
    public void ConstructorSelection_Performance_ShouldBeEfficient()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int iterations = 1000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            container.ServiceFactory.GetConstructor(typeof(MultiParameterService));
        }

        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should be very fast
    }

    [Theory]
    [InlineData(typeof(ParameterlessService))]
    [InlineData(typeof(SingleParameterService))]
    [InlineData(typeof(MultiParameterService))]
    [InlineData(typeof(PreferredConstructorService))]
    [InlineData(typeof(ComplexConstructorService))]
    public void GetConstructor_WithVariousServiceTypes_ShouldReturnValidConstructor(Type serviceType)
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var constructor = container.ServiceFactory.GetConstructor(serviceType);

        // Assert
        Assert.NotNull(constructor);
        Assert.Equal(serviceType, constructor!.DeclaringType);
    }

    [Fact]
    public void ConstructorSelection_WithServiceDependencies_ShouldCreateInstanceCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();

        // Act
        var service = container.GetRequiredService<IDependentService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.PrimaryDependency);
        Assert.IsType<TestService>(service.PrimaryDependency);
    }

    [Fact]
    public void GetConstructor_AlgorithmValidation_ShouldFollowCorrectPriority()
    {
        // This test validates the exact algorithm described in the implementation:
        // 1. Single constructor -> return it
        // 2. Preferred constructor -> return it
        // 3. Constructor with most parameters -> return it

        // Arrange
        using var container = new ServiceContainer();

        // Test 1: Single constructor
        var singleConstructor = container.ServiceFactory.GetConstructor(typeof(ParameterlessService));
        Assert.NotNull(singleConstructor);
        Assert.Empty(singleConstructor!.GetParameters());

        // Test 2: Preferred constructor (should override parameter count)
        var preferredConstructor = container.ServiceFactory.GetConstructor(typeof(PreferredConstructorService));
        Assert.NotNull(preferredConstructor);
        Assert.NotNull(preferredConstructor!.GetCustomAttribute<PreferredConstructorAttribute>());
        Assert.Equal(2, preferredConstructor.GetParameters().Length);

        // Test 3: Most parameters (no preferred constructor)
        var mostParamsConstructor = container.ServiceFactory.GetConstructor(typeof(ComplexConstructorService));
        Assert.NotNull(mostParamsConstructor);
        Assert.Null(mostParamsConstructor!.GetCustomAttribute<PreferredConstructorAttribute>());
        Assert.Equal(2, mostParamsConstructor.GetParameters().Length); // Should be the one with most parameters
    }
}