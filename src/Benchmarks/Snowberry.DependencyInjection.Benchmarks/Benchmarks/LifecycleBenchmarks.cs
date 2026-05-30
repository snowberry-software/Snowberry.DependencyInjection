using BenchmarkDotNet.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Benchmarks.Fixtures;

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>Container construction cost (the construction IS the measured work).</summary>
public class ContainerConstructionBenchmarks
{
    [Benchmark]
    public bool Construct_Empty()
    {
        var c = new ServiceContainer();
        bool disposed = c.IsDisposed;
        c.Dispose();
        return disposed;
    }

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
    [Params(8, 16)]
    public int N;

    private ServiceContainer _container = null!;

    [IterationSetup]
    public void IterationSetup() => _container = new ServiceContainer();

    [IterationCleanup]
    public void IterationCleanup() => _container.Dispose();

    [Benchmark]
    public void RegisterTransient_N()
    {
        for (int i = 0; i < N; i++)
            _container.Register(ServiceDescriptor.Transient(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i]));
    }

    [Benchmark]
    public void RegisterSingleton_N()
    {
        for (int i = 0; i < N; i++)
            _container.Register(ServiceDescriptor.Singleton(Markers.s_ServiceTypes[i], Markers.s_ImplTypes[i], singletonInstance: null));
    }

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

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterScoped<IServiceB, ServiceB>();
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public void CreateScope_Resolve_Dispose()
    {
        using var scope = _container.CreateScope();
        _ = scope.ServiceProvider.GetService<IServiceB>();
    }

    [Benchmark]
    public void CreateScope_Empty_Dispose()
    {
        using var scope = _container.CreateScope();
    }
}

/// <summary>Disposal cost of a scope/container holding N disposables (the LIFO drain). Registration cost is in IterationSetup, not measured.</summary>
public class DisposalBenchmarks
{
    [Params(100, 1000)]
    public int N;

    private ServiceContainer _container = null!;

    [IterationSetup]
    public void IterationSetup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IDisposableService, DisposableService>();
        for (int i = 0; i < N; i++)
            _ = _container.GetService<IDisposableService>();
    }

    [Benchmark]
    public void Dispose_N_Disposables() => _container.Dispose();
}
