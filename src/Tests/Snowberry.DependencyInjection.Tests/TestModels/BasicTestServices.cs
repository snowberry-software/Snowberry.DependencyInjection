using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Basic test service implementation.
/// </summary>
public class TestService : ITestService
{
    public string Name { get; set; } = "DefaultTestService";
    public bool IsDisposed { get; private set; }

    public void DoWork()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(TestService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Alternative test service implementation for testing multiple registrations.
/// </summary>
public class AlternativeTestService : ITestService
{
    public string Name { get; set; } = "AlternativeTestService";
    public bool IsDisposed { get; private set; }

    public void DoWork()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AlternativeTestService));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Test service with constructor dependency injection.
/// </summary>
public class DependentService : IDependentService
{
    public ITestService PrimaryDependency { get; }

    [Inject]
    public ITestService? OptionalDependency { get; set; }

    public bool IsDisposed { get; private set; }

    public DependentService(ITestService primaryDependency)
    {
        PrimaryDependency = primaryDependency ?? throw new ArgumentNullException(nameof(primaryDependency));
    }

    public string GetDependencyInfo()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(DependentService));

        return $"Primary: {PrimaryDependency.Name}, Optional: {OptionalDependency?.Name ?? "None"}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service with multiple dependencies for complex testing scenarios.
/// </summary>
public class ComplexService : IComplexService
{
    public ITestService TestService { get; }
    public IDependentService DependentService { get; }
    public bool IsDisposed { get; private set; }

    public ComplexService(ITestService testService, IDependentService dependentService)
    {
        TestService = testService ?? throw new ArgumentNullException(nameof(testService));
        DependentService = dependentService ?? throw new ArgumentNullException(nameof(dependentService));
    }

    public string ProcessData(string input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ComplexService));

        return $"Processed: {input} by {TestService.Name}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service implementation for testing keyed services.
/// </summary>
public class KeyedServiceA : IKeyedService
{
    public string ServiceKey => "KeyA";
    public bool IsDisposed { get; private set; }

    public string ProcessRequest(string request)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(KeyedServiceA));

        return $"ProcessedByA: {request}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Alternative keyed service implementation.
/// </summary>
public class KeyedServiceB : IKeyedService
{
    public string ServiceKey => "KeyB";
    public bool IsDisposed { get; private set; }

    public string ProcessRequest(string request)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(KeyedServiceB));

        return $"ProcessedByB: {request}";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service with both constructor and property injection.
/// </summary>
public class HybridService : IHybridService
{
    public ITestService ConstructorInjected { get; }
    public ITestService? PropertyInjected { get; set; }
    public bool IsDisposed { get; private set; }

    public HybridService(ITestService constructorInjected)
    {
        ConstructorInjected = constructorInjected ?? throw new ArgumentNullException(nameof(constructorInjected));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service that resolves dependencies via IServiceProvider in constructor.
/// Used for testing that scoped services resolved manually share the same instance.
/// </summary>
public class ServiceWithServiceProviderDependency : IDisposable
{
    public ITestService ResolvedService { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithServiceProviderDependency(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        // Manually resolve the service using the extension method
        ResolvedService = serviceProvider.GetRequiredService<ITestService>();
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Service that receives IServiceProvider and IScope to verify scope behavior.
/// Used for testing that services receive the correct scope context.
/// </summary>
public class ServiceWithScopeAndProviderDependencies : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public IScope Scope { get; }
    public bool IsDisposed { get; private set; }

    public ServiceWithScopeAndProviderDependencies(IServiceProvider serviceProvider, IScope scope)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}