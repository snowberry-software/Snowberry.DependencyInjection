using BenchmarkDotNet.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Benchmarks.Fixtures;
#if BENCH_COMPARE
using MsDI = Microsoft.Extensions.DependencyInjection;
#endif

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>Transient resolution across graph shapes. Container built once in GlobalSetup.</summary>
public class TransientResolutionBenchmarks
{
    private ServiceContainer _container = null!;
#if BENCH_COMPARE
    private IServiceProvider _ms = null!;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterTransient<IServiceB, ServiceB>();

        _container.RegisterTransient<IChain1, Chain1>();
        _container.RegisterTransient<IChain2, Chain2>();
        _container.RegisterTransient<IChain3, Chain3>();
        _container.RegisterTransient<IChain4, Chain4>();
        _container.RegisterTransient<IChain5, Chain5>();

        _container.RegisterTransient<IDep1, Dep1>();
        _container.RegisterTransient<IDep2, Dep2>();
        _container.RegisterTransient<IDep3, Dep3>();
        _container.RegisterTransient<IDep4, Dep4>();
        _container.RegisterTransient<IDep5, Dep5>();
        _container.RegisterTransient<IDep6, Dep6>();
        _container.RegisterTransient<IDep7, Dep7>();
        _container.RegisterTransient<IDep8, Dep8>();
        _container.RegisterTransient<IWideRoot, WideRoot>();

        _container.RegisterTransient<IPropTarget, PropTarget>();

#if BENCH_COMPARE
        var sc = new MsDI.ServiceCollection();
        MsDI.ServiceCollectionServiceExtensions.AddTransient<IServiceA, ServiceA>(sc);
        MsDI.ServiceCollectionServiceExtensions.AddTransient<IServiceB, ServiceB>(sc);
        _ms = MsDI.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(sc);
#endif
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public IServiceA Resolve_NoDeps() => _container.GetService<IServiceA>()!;

    [Benchmark]
    public IServiceB Resolve_OneDep() => _container.GetService<IServiceB>()!;

    [Benchmark]
    public IChain5 Resolve_DeepChain() => _container.GetService<IChain5>()!;

    [Benchmark]
    public IWideRoot Resolve_WideDeps() => _container.GetService<IWideRoot>()!;

    [Benchmark]
    public IPropTarget Resolve_PropertyInjection() => _container.GetService<IPropTarget>()!;

#if BENCH_COMPARE
    [Benchmark]
    public IServiceA Ms_Resolve_NoDeps() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceA>(_ms);

    [Benchmark]
    public IServiceB Ms_Resolve_OneDep() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_ms);
#endif
}

/// <summary>Frozen (opt-in lock-in) transient resolution through the full-graph-inlining pipeline.</summary>
public class FrozenResolutionBenchmarks
{
    private ServiceContainer _container = null!;
#if BENCH_COMPARE
    private IServiceProvider _ms = null!;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterTransient<IServiceB, ServiceB>();

        _container.RegisterTransient<IChain1, Chain1>();
        _container.RegisterTransient<IChain2, Chain2>();
        _container.RegisterTransient<IChain3, Chain3>();
        _container.RegisterTransient<IChain4, Chain4>();
        _container.RegisterTransient<IChain5, Chain5>();

        _container.RegisterTransient<IDep1, Dep1>();
        _container.RegisterTransient<IDep2, Dep2>();
        _container.RegisterTransient<IDep3, Dep3>();
        _container.RegisterTransient<IDep4, Dep4>();
        _container.RegisterTransient<IDep5, Dep5>();
        _container.RegisterTransient<IDep6, Dep6>();
        _container.RegisterTransient<IDep7, Dep7>();
        _container.RegisterTransient<IDep8, Dep8>();
        _container.RegisterTransient<IWideRoot, WideRoot>();

        _container.Freeze();

#if BENCH_COMPARE
        var sc = new MsDI.ServiceCollection();
        MsDI.ServiceCollectionServiceExtensions.AddTransient<IServiceA, ServiceA>(sc);
        MsDI.ServiceCollectionServiceExtensions.AddTransient<IServiceB, ServiceB>(sc);
        _ms = MsDI.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(sc);
#endif
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public IServiceA Resolve_NoDeps_Frozen() => _container.GetService<IServiceA>()!;

    [Benchmark]
    public IServiceB Resolve_OneDep_Frozen() => _container.GetService<IServiceB>()!;

    [Benchmark]
    public IChain5 Resolve_DeepChain_Frozen() => _container.GetService<IChain5>()!;

    [Benchmark]
    public IWideRoot Resolve_WideDeps_Frozen() => _container.GetService<IWideRoot>()!;

#if BENCH_COMPARE
    [Benchmark]
    public IServiceA Ms_Resolve_NoDeps() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceA>(_ms);

    [Benchmark]
    public IServiceB Ms_Resolve_OneDep() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_ms);
#endif
}

/// <summary>Steady-state (warmed) singleton resolution, the common case.</summary>
public class SingletonResolutionBenchmarks
{
    private ServiceContainer _container = null!;
#if BENCH_COMPARE
    private IServiceProvider _ms = null!;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterSingleton<IServiceA, ServiceA>();
        _container.RegisterSingleton<IServiceB, ServiceB>();
        _ = _container.GetService<IServiceB>(); // warm the singleton cache

#if BENCH_COMPARE
        var sc = new MsDI.ServiceCollection();
        MsDI.ServiceCollectionServiceExtensions.AddSingleton<IServiceA, ServiceA>(sc);
        MsDI.ServiceCollectionServiceExtensions.AddSingleton<IServiceB, ServiceB>(sc);
        _ms = MsDI.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(sc);
        _ = MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_ms);
#endif
    }

    [GlobalCleanup]
    public void Cleanup() => _container.Dispose();

    [Benchmark]
    public IServiceB Resolve_Singleton_Cached() => _container.GetService<IServiceB>()!;

#if BENCH_COMPARE
    [Benchmark]
    public IServiceB Ms_Resolve_Singleton_Cached() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_ms);
#endif
}

/// <summary>Steady-state scoped resolution within a warmed scope.</summary>
public class ScopedResolutionBenchmarks
{
    private ServiceContainer _container = null!;
    private IScope _scope = null!;
#if BENCH_COMPARE
    private MsDI.IServiceScope _msScope = null!;
#endif

    [GlobalSetup]
    public void Setup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IServiceA, ServiceA>();
        _container.RegisterScoped<IServiceB, ServiceB>();
        _scope = _container.CreateScope();
        _ = _scope.ServiceProvider.GetService<IServiceB>(); // warm the scoped cache

#if BENCH_COMPARE
        var sc = new MsDI.ServiceCollection();
        MsDI.ServiceCollectionServiceExtensions.AddTransient<IServiceA, ServiceA>(sc);
        MsDI.ServiceCollectionServiceExtensions.AddScoped<IServiceB, ServiceB>(sc);
        var provider = MsDI.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(sc);
        _msScope = MsDI.ServiceProviderServiceExtensions.CreateScope(provider);
        _ = MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_msScope.ServiceProvider);
#endif
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _container.Dispose();
    }

    [Benchmark]
    public IServiceB Resolve_Scoped_WithinScope_Cached() => _scope.ServiceProvider.GetService<IServiceB>()!;

#if BENCH_COMPARE
    [Benchmark]
    public IServiceB Ms_Resolve_Scoped_WithinScope_Cached() => MsDI.ServiceProviderServiceExtensions.GetRequiredService<IServiceB>(_msScope.ServiceProvider);
#endif
}
