using BenchmarkDotNet.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Benchmarks.Fixtures;

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>Container construction cost (the construction IS the measured work).</summary>
public class ContainerConstructionBenchmarks
{
    /// <summary>Constructs and disposes an empty container.</summary>
    /// <returns>Whether the container reported itself disposed before disposal, so the work is not optimized away.</returns>
    [Benchmark]
    public bool Construct_Empty()
    {
        var c = new ServiceContainer();
        bool disposed = c.IsDisposed;
        c.Dispose();
        return disposed;
    }

    /// <summary>Constructs a container, registers three services and disposes it.</summary>
    /// <returns>Whether the container reported itself disposed before disposal, so the work is not optimized away.</returns>
    [Benchmark]
    public bool Construct_RegisterFew()
    {
        var c = new ServiceContainer();
        c.RegisterSingleton<IServiceA, ServiceA>();
        c.RegisterTransient<IServiceB, ServiceB>();
        c.RegisterScoped<IDep1, Dep1>();
        bool disposed = c.IsDisposed;
        c.Dispose();
        return disposed;
    }
}

/// <summary>Registration throughput. Fresh registry per iteration (container build is not measured).</summary>
public class RegistrationBenchmarks
{
    /// <summary>The number of services registered per iteration.</summary>
    [Params(8, 16)]
    public int N;

    private ServiceContainer _container = null!;

    /// <summary>Creates an empty container for the iteration.</summary>
    [IterationSetup]
    public void IterationSetup() => _container = new ServiceContainer();

    /// <summary>Disposes the iteration's container.</summary>
    [IterationCleanup]
    public void IterationCleanup() => _container.Dispose();

    /// <summary>Registers <see cref="N"/> transient services.</summary>
    [Benchmark]
    public void RegisterTransient_N()
    {
        for (int i = 0; i < N; i++)
            _container.Register(ServiceDescriptor.Transient(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i]));
    }

    /// <summary>Registers <see cref="N"/> singleton services.</summary>
    [Benchmark]
    public void RegisterSingleton_N()
    {
        for (int i = 0; i < N; i++)
            _container.Register(ServiceDescriptor.Singleton(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i], singletonInstance: null));
    }

    /// <summary>Registers <see cref="N"/> scoped services.</summary>
    [Benchmark]
    public void RegisterScoped_N()
    {
        for (int i = 0; i < N; i++)
            _container.Register(ServiceDescriptor.Scoped(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i]));
    }
}

/// <summary>The "web request" shape: create scope, resolve, dispose. Scope lifecycle is the measured work.</summary>
public class ScopeLifecycleBenchmarks
{
    private ServiceContainer _container = null!;

    /// <summary>Builds the container used by every iteration.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterScoped<IServiceB, ServiceB>();
    }

    /// <summary>Disposes the container.</summary>
    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    /// <summary>Creates a scope, resolves one scoped service with a transient dependency, and disposes the scope.</summary>
    [Benchmark]
    public void CreateScope_Resolve_Dispose()
    {
        using var scope = _container.CreateScope();
        _ = scope.ServiceProvider.GetService<IServiceB>();
    }

    /// <summary>Creates and disposes a scope without resolving anything.</summary>
    [Benchmark]
    public void CreateScope_Empty_Dispose()
    {
        using var scope = _container.CreateScope();
    }
}

/// <summary>
/// A scope that resolves many scoped services (a request touching 16 scoped dependencies). Measures how the
/// per-scope instance cache scales with the number of scoped instances it holds.
/// </summary>
public class WideScopeBenchmarks
{
    private ServiceContainer _container = null!;

    /// <summary>Builds the container used by every iteration.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        for (int i = 0; i < Markers.c_Count; i++)
            _container.Register(ServiceDescriptor.Scoped(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i]));
    }

    /// <summary>Disposes the container.</summary>
    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    /// <summary>Creates a scope, resolves 16 distinct scoped services, and disposes the scope.</summary>
    [Benchmark]
    public void CreateScope_Resolve16Scoped_Dispose()
    {
        using var scope = _container.CreateScope();
        var provider = scope.ServiceProvider;
        var types = Markers.s_ServiceTypes;
        for (int i = 0; i < types.Length; i++)
            _ = provider.GetService(types[i]);
    }
}

/// <summary>Disposal cost of a scope/container holding N disposables (the LIFO drain). Registration cost is in IterationSetup, not measured.</summary>
public class DisposalBenchmarks
{
    /// <summary>The number of disposables the container tracks.</summary>
    [Params(100, 1000)]
    public int N;

    private ServiceContainer _container = null!;

    /// <summary>Creates a container that tracks <see cref="N"/> disposable transients.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IDisposableService, DisposableService>();
        for (int i = 0; i < N; i++)
            _ = _container.GetService<IDisposableService>();
    }

    /// <summary>Disposes the container and with it the tracked disposables.</summary>
    [Benchmark]
    public void Dispose_N_Disposables() => _container.Dispose();
}
