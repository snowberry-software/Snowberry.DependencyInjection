using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Service for testing basic property injection with required dependencies.
/// </summary>
public class PropertyInjectionService : IDisposable
{
    public ITestService ConstructorDependency { get; }

    [Inject]
    public ITestService? RequiredPropertyDependency { get; set; }

    [Inject(isRequired: false)]
    public ITestService? OptionalPropertyDependency { get; set; }

    public bool IsDisposed { get; private set; }

    public PropertyInjectionService(ITestService constructorDependency)
    {
        ConstructorDependency = constructorDependency ?? throw new ArgumentNullException(nameof(constructorDependency));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing property injection with multiple different service types.
/// </summary>
public class MultiplePropertyInjectionService : IDisposable
{
    [Inject]
    public ITestService? TestService { get; set; }

    [Inject]
    public IDependentService? DependentService { get; set; }

    [Inject]
    public IComplexService? ComplexService { get; set; }

    [Inject(isRequired: false)]
    public IKeyedService? OptionalKeyedService { get; set; }

    public string Value { get; set; } = "Default";
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing property injection with read-only properties.
/// </summary>
public class ReadOnlyPropertyService : IDisposable
{
    [Inject]
    public ITestService? TestService { get; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing property injection with private setters.
/// </summary>
public class PrivateSetterPropertyService : IDisposable
{
    [Inject]
    public ITestService? TestService { get; private set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing property injection with different property types.
/// </summary>
public class MixedPropertyTypeService : IDisposable
{
    [Inject]
    public ITestService? InterfaceProperty { get; set; }

    [Inject]
    public TestService? ConcreteProperty { get; set; }

    // Property without [Inject] attribute - should not be injected
    public ITestService? NonInjectedProperty { get; set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing property injection inheritance.
/// </summary>
public abstract class BasePropertyInjectionService : IDisposable
{
    [Inject]
    public ITestService? BaseProperty { get; set; }

    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Derived service to test property injection inheritance.
/// </summary>
public class DerivedPropertyInjectionService : BasePropertyInjectionService
{
    [Inject]
    public IDependentService? DerivedProperty { get; set; }

    public override void Dispose()
    {
        base.Dispose();
    }
}

/// <summary>
/// Service for testing property injection with generic types.
/// </summary>
public class GenericPropertyInjectionService : IDisposable
{
    [Inject]
    public IRepository<TestEntity>? Repository { get; set; }

    [Inject]
    public IGenericProcessor<string>? StringProcessor { get; set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}