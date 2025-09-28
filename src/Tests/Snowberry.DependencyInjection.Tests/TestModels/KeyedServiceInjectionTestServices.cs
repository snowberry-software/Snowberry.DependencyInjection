using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Service for testing keyed service injection in constructor parameters.
/// </summary>
public class KeyedConstructorInjectionService : IDisposable
{
    public IKeyedService PrimaryService { get; }
    public IKeyedService SecondaryService { get; }
    public bool IsDisposed { get; private set; }

    public KeyedConstructorInjectionService(
        [FromKeyedServices("primary")] IKeyedService primaryService,
        [FromKeyedServices("secondary")] IKeyedService secondaryService)
    {
        PrimaryService = primaryService ?? throw new ArgumentNullException(nameof(primaryService));
        SecondaryService = secondaryService ?? throw new ArgumentNullException(nameof(secondaryService));
    }

    public string GetServiceKeys()
    {
        return $"Primary: {PrimaryService.ServiceKey}, Secondary: {SecondaryService.ServiceKey}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing keyed service injection in properties.
/// </summary>
public class KeyedPropertyInjectionService : IDisposable
{
    [Inject]
    [FromKeyedServices("keyed-test")]
    public ITestService? KeyedTestService { get; set; }

    [Inject]
    [FromKeyedServices("keyed-dependent")]
    public IDependentService? KeyedDependentService { get; set; }

    [Inject(isRequired: false)]
    [FromKeyedServices("optional-keyed")]
    public IComplexService? OptionalKeyedService { get; set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing mixed keyed and non-keyed dependency injection.
/// </summary>
public class MixedKeyedInjectionService : IDisposable
{
    public ITestService DefaultService { get; }
    public ITestService KeyedService { get; }

    [Inject]
    public IDependentService? DefaultPropertyService { get; set; }

    [Inject]
    [FromKeyedServices("mixed-key")]
    public IDependentService? KeyedPropertyService { get; set; }

    public bool IsDisposed { get; private set; }

    public MixedKeyedInjectionService(
        ITestService defaultService,
        [FromKeyedServices("constructor-key")] ITestService keyedService)
    {
        DefaultService = defaultService ?? throw new ArgumentNullException(nameof(defaultService));
        KeyedService = keyedService ?? throw new ArgumentNullException(nameof(keyedService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing keyed service injection with null keys.
/// </summary>
public class NullKeyedInjectionService : IDisposable
{
    public ITestService NullKeyedService { get; }

    [Inject]
    [FromKeyedServices(null)]
    public IDependentService? NullKeyedProperty { get; set; }

    public bool IsDisposed { get; private set; }

    public NullKeyedInjectionService([FromKeyedServices(null)] ITestService nullKeyedService)
    {
        NullKeyedService = nullKeyedService ?? throw new ArgumentNullException(nameof(nullKeyedService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing keyed service injection with various key types.
/// </summary>
public class VariousKeyTypeInjectionService : IDisposable
{
    public ITestService StringKeyedService { get; }
    public ITestService IntKeyedService { get; }
    public ITestService BoolKeyedService { get; }

    [Inject]
    [FromKeyedServices(3.14)]
    public ITestService? DoubleKeyedProperty { get; set; }

    [Inject]
    [FromKeyedServices(typeof(TestService))]
    public ITestService? TypeKeyedProperty { get; set; }

    public bool IsDisposed { get; private set; }

    public VariousKeyTypeInjectionService(
        [FromKeyedServices("string-key")] ITestService stringKeyedService,
        [FromKeyedServices(42)] ITestService intKeyedService,
        [FromKeyedServices(true)] ITestService boolKeyedService)
    {
        StringKeyedService = stringKeyedService ?? throw new ArgumentNullException(nameof(stringKeyedService));
        IntKeyedService = intKeyedService ?? throw new ArgumentNullException(nameof(intKeyedService));
        BoolKeyedService = boolKeyedService ?? throw new ArgumentNullException(nameof(boolKeyedService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing keyed service injection with generic services.
/// </summary>
public class KeyedGenericInjectionService : IDisposable
{
    [Inject]
    [FromKeyedServices("generic-repo")]
    public IRepository<TestEntity>? KeyedRepository { get; set; }

    [Inject]
    [FromKeyedServices("generic-processor")]
    public IGenericProcessor<string>? KeyedProcessor { get; set; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing complex keyed service scenarios with dependency chains.
/// </summary>
public class ComplexKeyedDependencyService : IDisposable
{
    public ITestService BaseService { get; }

    [Inject]
    [FromKeyedServices("chain-start")]
    public IDependentService? ChainStartService { get; set; }

    [Inject]
    [FromKeyedServices("chain-end")]
    public IComplexService? ChainEndService { get; set; }

    public bool IsDisposed { get; private set; }

    public ComplexKeyedDependencyService([FromKeyedServices("base-service")] ITestService baseService)
    {
        BaseService = baseService ?? throw new ArgumentNullException(nameof(baseService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service for testing keyed service injection inheritance.
/// </summary>
public abstract class BaseKeyedInjectionService : IDisposable
{
    [Inject]
    [FromKeyedServices("base-keyed")]
    public ITestService? BaseKeyedService { get; set; }

    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Derived service to test keyed service injection inheritance.
/// </summary>
public class DerivedKeyedInjectionService : BaseKeyedInjectionService
{
    [Inject]
    [FromKeyedServices("derived-keyed")]
    public IDependentService? DerivedKeyedService { get; set; }

    public override void Dispose()
    {
        base.Dispose();
    }
}

/// <summary>
/// Enhanced keyed service implementations for testing.
/// </summary>
public class PrimaryKeyedService : IKeyedService
{
    public string ServiceKey => "Primary";
    public bool IsDisposed { get; private set; }

    public string ProcessRequest(string request)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(PrimaryKeyedService));

        return $"Primary: {request}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Secondary keyed service implementation for testing.
/// </summary>
public class SecondaryKeyedService : IKeyedService
{
    public string ServiceKey => "Secondary";
    public bool IsDisposed { get; private set; }

    public string ProcessRequest(string request)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SecondaryKeyedService));

        return $"Secondary: {request}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}