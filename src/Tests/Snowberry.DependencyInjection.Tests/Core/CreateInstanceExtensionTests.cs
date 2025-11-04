using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the CreateInstance extension methods on IServiceProvider.
/// </summary>
public class CreateInstanceExtensionTests
{
    [Fact]
    public void CreateInstance_WithNoParameters_CreatesInstance()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var instance = container.CreateInstance<SimpleServiceWithNoParams>();

        // Assert
        Assert.NotNull(instance);
        Assert.IsType<SimpleServiceWithNoParams>(instance);
    }

    [Fact]
    public void CreateInstance_WithSingleDependency_InjectsDependency()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        var instance = container.CreateInstance<ServiceWithOneDependency>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.DependencyA);
    }

    [Fact]
    public void CreateInstance_WithMultipleDependencies_InjectsAllDependencies()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();
        container.RegisterSingleton<DependencyB>();
        container.RegisterSingleton<DependencyC>();

        // Act
        var instance = container.CreateInstance<ServiceWithMultipleDependencies>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.DependencyA);
        Assert.NotNull(instance.DependencyB);
        Assert.NotNull(instance.DependencyC);
    }

    [Fact]
    public void CreateInstance_WithPropertyInjection_InjectsProperties()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();
        container.RegisterSingleton<DependencyB>();

        // Act
        var instance = container.CreateInstance<ServiceWithPropertyInjection>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.PropertyA);
        Assert.NotNull(instance.PropertyB);
    }

    [Fact]
    public void CreateInstance_NonGeneric_CreatesCorrectType()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        object instance = container.CreateInstance(typeof(ServiceWithOneDependency));

        // Assert
        Assert.NotNull(instance);
        Assert.IsType<ServiceWithOneDependency>(instance);
        var typedInstance = (ServiceWithOneDependency)instance;
        Assert.NotNull(typedInstance.DependencyA);
    }

    [Fact]
    public void CreateInstance_NonGeneric_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => container.CreateInstance(null!));
    }

    [Fact]
    public void CreateInstance_WithGenericTypeArguments_CreatesClosedGenericType()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        var instance = (OpenGenericService<string>)container.CreateInstance(typeof(OpenGenericService<>), genericTypeArguments: new[] { typeof(string) });

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("String", instance.GetTypeName());
    }

    [Fact]
    public void CreateInstance_NonGeneric_WithGenericTypeArguments_CreatesClosedGenericType()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        object instance = container.CreateInstance(typeof(OpenGenericService<>), genericTypeArguments: new[] { typeof(int) });

        // Assert
        Assert.NotNull(instance);
        Assert.IsType<OpenGenericService<int>>(instance);
        var typedInstance = (OpenGenericService<int>)instance;
        Assert.Equal("Int32", typedInstance.GetTypeName());
    }

    [Fact]
    public void CreateInstance_WithNestedDependencies_ResolvesRecursively()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();
        container.RegisterSingleton<ServiceWithOneDependency>();

        // Act
        var instance = container.CreateInstance<ServiceWithNestedDependency>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.ServiceWithOneDependency);
        Assert.NotNull(instance.ServiceWithOneDependency.DependencyA);
    }

    [Fact]
    public void CreateInstance_MultipleGenericParameters_CreatesCorrectType()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var instance = container.CreateInstance<MultiGenericService<string, int>>();

        // Assert
        Assert.NotNull(instance);
        Assert.Equal("String - Int32", instance.GetTypeNames());
    }

    [Fact]
    public void CreateInstance_WithConstructorAndPropertyInjection_InjectsBoth()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();
        container.RegisterSingleton<DependencyB>();
        container.RegisterSingleton<DependencyC>();

        // Act
        var instance = container.CreateInstance<ServiceWithBothInjectionTypes>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.ConstructorDependency);
        Assert.NotNull(instance.PropertyDependency);
        Assert.NotNull(instance.AnotherPropertyDependency);
    }

    [Fact]
    public void CreateInstance_OptionalPropertyDependency_DoesNotThrowWhenUnregistered()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        var instance = container.CreateInstance<ServiceWithOptionalPropertyInjection>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.RequiredProperty);
        Assert.Null(instance.OptionalProperty);
    }

    [Fact]
    public void CreateInstance_FromScope_UsesCorrectServiceProvider()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<DependencyA>();

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var instance1 = scope1.ServiceProvider.CreateInstance<ServiceWithOneDependency>();
        var instance2 = scope2.ServiceProvider.CreateInstance<ServiceWithOneDependency>();

        // Assert
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotSame(instance1.DependencyA, instance2.DependencyA);
    }

    [Fact]
    public void CreateInstance_AlwaysCreatesNewInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        var instance1 = container.CreateInstance<ServiceWithOneDependency>();
        var instance2 = container.CreateInstance<ServiceWithOneDependency>();

        // Assert
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotSame(instance1, instance2);
        Assert.Same(instance1.DependencyA, instance2.DependencyA); // But dependencies are same (singleton)
    }

    [Fact]
    public void CreateInstance_WithInterface_ThrowsException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<InvalidServiceImplementationType>(() => container.CreateInstance<ITestInterface>());
    }

    [Fact]
    public void CreateInstance_WithAbstractClass_ThrowsException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<InvalidServiceImplementationType>(() => container.CreateInstance<AbstractTestClass>());
    }

    [Fact]
    public void CreateInstance_ComplexGenericType_CreatesCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var instance = container.CreateInstance<ComplexGenericService<List<Dictionary<string, int>>>>();

        // Assert
        Assert.NotNull(instance);
        Assert.Contains("List", instance.GetTypeName());
    }

    [Fact]
    public void CreateInstance_WithPreferredConstructor_UsesPreferredConstructor()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>();

        // Act
        var instance = container.CreateInstance<ServiceWithPreferredConstructor>();

        // Assert
        Assert.NotNull(instance);
        Assert.True(instance.UsedPreferredConstructor);
    }

    [Fact]
    public void CreateInstance_WithDisposableService_ServiceIsNotDisposedWithContainer()
    {
        // Arrange
        DisposableTestService? instance;
        using (var container = new ServiceContainer())
        {
            // Act
            instance = container.CreateInstance<DisposableTestService>();
            Assert.False(instance.IsDisposed);
        }

        // Assert
        Assert.False(instance.IsDisposed);
    }

    [Fact]
    public void CreateInstance_FromServiceFactory_CreatesInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        var serviceFactory = container.GetRequiredService<IServiceFactory>();

        // Act
        var instance = serviceFactory.CreateInstance<SimpleServiceWithNoParams>(container);

        // Assert
        Assert.NotNull(instance);
        Assert.IsType<SimpleServiceWithNoParams>(instance);
    }

    [Fact]
    public void CreateInstance_FromServiceFactory_NonGeneric_CreatesInstance()
    {
        // Arrange
        using var container = new ServiceContainer();
        var serviceFactory = container.GetRequiredService<IServiceFactory>();

        // Act
        object instance = serviceFactory.CreateInstance(typeof(SimpleServiceWithNoParams), container);

        // Assert
        Assert.NotNull(instance);
        Assert.IsType<SimpleServiceWithNoParams>(instance);
    }

    [Fact]
    public void CreateInstance_RecursiveGenericTypes_CreatesCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var instance = container.CreateInstance<RecursiveGenericService<int>>();

        // Assert
        Assert.NotNull(instance);
        Assert.Equal(typeof(int), instance.GetInnerType());
    }

    [Fact]
    public void CreateInstance_ValueTypeParameter_UsesDefaultValue()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act
        var instance = container.CreateInstance<ServiceWithValueTypeParam>();

        // Assert
        Assert.NotNull(instance);
        Assert.Equal(0, instance.Value);
    }

    [Fact]
    public void CreateInstance_WithKeyedDependency_InjectsKeyedService()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<DependencyA>("keyA");
        container.RegisterSingleton<DependencyB>("keyB");

        // Act
        var instance = container.CreateInstance<ServiceWithKeyedDependencies>();

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.DependencyA);
        Assert.NotNull(instance.DependencyB);
    }

    // Test helper classes
    private class SimpleServiceWithNoParams
    {
        public string Message => "Hello";
    }

    private class DependencyA { }
    private class DependencyB { }
    private class DependencyC { }

    private class ServiceWithOneDependency
    {
        public DependencyA DependencyA { get; }

        public ServiceWithOneDependency(DependencyA dependencyA)
        {
            DependencyA = dependencyA ?? throw new ArgumentNullException(nameof(dependencyA));
        }
    }

    private class ServiceWithMultipleDependencies
    {
        public DependencyA DependencyA { get; }
        public DependencyB DependencyB { get; }
        public DependencyC DependencyC { get; }

        public ServiceWithMultipleDependencies(DependencyA dependencyA, DependencyB dependencyB, DependencyC dependencyC)
        {
            DependencyA = dependencyA ?? throw new ArgumentNullException(nameof(dependencyA));
            DependencyB = dependencyB ?? throw new ArgumentNullException(nameof(dependencyB));
            DependencyC = dependencyC ?? throw new ArgumentNullException(nameof(dependencyC));
        }
    }

    private class ServiceWithPropertyInjection
    {
        [Inject]
        public DependencyA? PropertyA { get; set; }

        [Inject]
        public DependencyB? PropertyB { get; set; }
    }

    private class OpenGenericService<T>
    {
        public string GetTypeName()
        {
            return typeof(T).Name;
        }
    }

    private class ServiceWithNestedDependency
    {
        public ServiceWithOneDependency ServiceWithOneDependency { get; }

        public ServiceWithNestedDependency(ServiceWithOneDependency serviceWithOneDependency)
        {
            ServiceWithOneDependency = serviceWithOneDependency ?? throw new ArgumentNullException(nameof(serviceWithOneDependency));
        }
    }

    private class MultiGenericService<T1, T2>
    {
        public string GetTypeNames()
        {
            return $"{typeof(T1).Name} - {typeof(T2).Name}";
        }
    }

    private class ServiceWithBothInjectionTypes
    {
        public DependencyA ConstructorDependency { get; }

        [Inject]
        public DependencyB? PropertyDependency { get; set; }

        [Inject]
        public DependencyC? AnotherPropertyDependency { get; set; }

        public ServiceWithBothInjectionTypes(DependencyA constructorDependency)
        {
            ConstructorDependency = constructorDependency ?? throw new ArgumentNullException(nameof(constructorDependency));
        }
    }

    private class ServiceWithOptionalPropertyInjection
    {
        [Inject]
        public DependencyA? RequiredProperty { get; set; }

        [Inject(isRequired: false)]
        public DependencyB? OptionalProperty { get; set; }
    }

    private interface ITestInterface { }

    private abstract class AbstractTestClass { }

    private class ComplexGenericService<T>
    {
        public string GetTypeName()
        {
            return typeof(T).Name;
        }
    }

    private class ServiceWithPreferredConstructor
    {
        public bool UsedPreferredConstructor { get; }

        public ServiceWithPreferredConstructor()
        {
            UsedPreferredConstructor = false;
        }

        [PreferredConstructor]
        public ServiceWithPreferredConstructor(DependencyA dependencyA)
        {
            UsedPreferredConstructor = true;
        }
    }

    private class DisposableTestService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private class RecursiveGenericService<T>
    {
        public Type GetInnerType()
        {
            return typeof(T);
        }
    }

    private class ServiceWithValueTypeParam
    {
        public int Value { get; }

        public ServiceWithValueTypeParam(int value = 0)
        {
            Value = value;
        }
    }

    private class ServiceWithKeyedDependencies
    {
        public DependencyA DependencyA { get; }
        public DependencyB DependencyB { get; }

        public ServiceWithKeyedDependencies(
            [FromKeyedServices("keyA")] DependencyA dependencyA,
            [FromKeyedServices("keyB")] DependencyB dependencyB)
        {
            DependencyA = dependencyA ?? throw new ArgumentNullException(nameof(dependencyA));
            DependencyB = dependencyB ?? throw new ArgumentNullException(nameof(dependencyB));
        }
    }
}
