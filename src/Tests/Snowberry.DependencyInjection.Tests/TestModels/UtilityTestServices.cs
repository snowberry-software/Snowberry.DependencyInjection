using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Simple entity for testing generic services.
/// </summary>
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service with no constructor parameters.
/// </summary>
public class ParameterlessService
{
    public string ServiceId { get; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Service with a single constructor parameter.
/// </summary>
public class SingleParameterService
{
    public string Value { get; }

    public SingleParameterService(string value)
    {
        Value = value;
    }
}

/// <summary>
/// Service with multiple constructor parameters.
/// </summary>
public class MultiParameterService
{
    public string StringValue { get; }
    public int IntValue { get; }
    public bool BoolValue { get; }

    public MultiParameterService(string stringValue, int intValue, bool boolValue)
    {
        StringValue = stringValue;
        IntValue = intValue;
        BoolValue = boolValue;
    }
}

/// <summary>
/// Service with multiple constructors and preferred constructor attribute.
/// </summary>
public class PreferredConstructorService
{
    public string Value { get; }
    public int? OptionalValue { get; }

    public PreferredConstructorService(string value)
    {
        Value = value;
    }

    [PreferredConstructor]
    public PreferredConstructorService(string value, int optionalValue) : this(value)
    {
        OptionalValue = optionalValue;
    }
}

/// <summary>
/// Service with dependency injection in constructor.
/// </summary>
public class ServiceWithDependencies : IDisposable
{
    public ITestService TestService { get; }
    public IDependentService? OptionalService { get; set; }
    public bool IsDisposed { get; private set; }

    public ServiceWithDependencies(ITestService testService)
    {
        TestService = testService ?? throw new ArgumentNullException(nameof(testService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service with complex constructor selection scenarios - tests parameter count preference.
/// </summary>
public class ComplexConstructorService
{
    public ITestService? TestService { get; }
    public IDependentService? DependentService { get; }
    public int ConstructorUsed { get; }

    // Parameterless constructor
    public ComplexConstructorService()
    {
        ConstructorUsed = 0;
    }

    // Constructor with one dependency
    public ComplexConstructorService(ITestService testService) : this()
    {
        TestService = testService;
        ConstructorUsed = 1;
    }

    // Constructor with two dependencies - should be preferred when both are available
    public ComplexConstructorService(ITestService testService, IDependentService dependentService) : this()
    {
        TestService = testService;
        DependentService = dependentService;
        ConstructorUsed = 2;
    }
}

/// <summary>
/// Service with explicit preferred constructor attribute.
/// </summary>
public class PreferredConstructorComplexService
{
    public ITestService? TestService { get; }
    public IDependentService? DependentService { get; }
    public int ConstructorUsed { get; }

    // Constructor with one dependency
    public PreferredConstructorComplexService(ITestService testService)
    {
        TestService = testService;
        ConstructorUsed = 1;
    }

    // Constructor with two dependencies but marked as preferred
    [PreferredConstructor]
    public PreferredConstructorComplexService(ITestService testService, IDependentService dependentService)
    {
        TestService = testService;
        DependentService = dependentService;
        ConstructorUsed = 2;
    }
}

/// <summary>
/// Service that tests fallback behavior when preferred constructor dependencies are unavailable.
/// </summary>
public class FallbackConstructorService
{
    public ITestService? TestService { get; }
    public IComplexService? ComplexService { get; }
    public int ConstructorUsed { get; }

    // Parameterless constructor - fallback
    public FallbackConstructorService()
    {
        ConstructorUsed = 0;
    }

    // Constructor with available dependency
    public FallbackConstructorService(ITestService testService) : this()
    {
        TestService = testService;
        ConstructorUsed = 1;
    }

    // Preferred constructor but requires unavailable dependency
    [PreferredConstructor]
    public FallbackConstructorService(ITestService testService, IComplexService complexService) : this()
    {
        TestService = testService;
        ComplexService = complexService;
        ConstructorUsed = 2;
    }
}