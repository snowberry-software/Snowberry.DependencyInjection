using BenchmarkDotNet.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Benchmarks.Fixtures;

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>Keyed resolution (non-null service key path). Snowberry-only — no honest MS.DI 1:1.</summary>
public class KeyedResolutionBenchmarks
{
    private ServiceContainer _container = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterSingleton<IKeyed, KeyedA>(serviceKey: "a");
        _container.RegisterSingleton<IKeyed, KeyedB>(serviceKey: "b");
        _container.RegisterTransient<IServiceA, ServiceA>(serviceKey: "t");
        _ = _container.GetKeyedService<IKeyed>("a"); // warm
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public IKeyed Resolve_Keyed_Singleton() => _container.GetKeyedService<IKeyed>("a")!;

    [Benchmark]
    public IServiceA Resolve_Keyed_Transient() => _container.GetKeyedService<IServiceA>("t")!;
}

/// <summary>Open-generic resolution. The first close materializes+caches a closed descriptor; steady-state hits the cache.</summary>
public class OpenGenericResolutionBenchmarks
{
    private ServiceContainer _container = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.Register(ServiceDescriptor.Transient(typeof(IRepository<>), typeof(Repository<>)));
        _ = _container.GetService<IRepository<BenchEntity>>(); // warm: materialize the closed descriptor
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public IRepository<BenchEntity> Resolve_OpenGeneric_Cached() => _container.GetService<IRepository<BenchEntity>>()!;
}

/// <summary>CreateInstance on an unregistered type — the reflection/activation path (B1's target). Metadata warmed in setup.</summary>
public class CreateInstanceBenchmarks
{
    private ServiceContainer _container = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterTransient<IServiceB, ServiceB>();
        _ = _container.CreateInstance<ActivatedWithCtor>(); // warm metadata
        _ = _container.CreateInstance<Hybrid>();
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public ActivatedWithCtor CreateInstance_CtorInjection() => _container.CreateInstance<ActivatedWithCtor>();

    [Benchmark]
    public Hybrid CreateInstance_CtorAndProperty() => _container.CreateInstance<Hybrid>();
}
