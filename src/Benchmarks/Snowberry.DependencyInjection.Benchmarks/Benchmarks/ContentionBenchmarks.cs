using BenchmarkDotNet.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Benchmarks.Fixtures;

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>
/// Parallel scoped resolution across many scopes — the scenario B2 targets. A single-threaded bench
/// cannot reveal a lock-contention fix, so this drives N threads each creating scopes and resolving a
/// scoped service. ThreadingDiagnoser reports lock contentions (expected to drop sharply after B2).
/// </summary>
[ThreadingDiagnoser]
public class ParallelScopedResolutionBenchmarks
{
    private const int ScopesPerThread = 200;

    [Params(1, 4, 8)]
    public int Threads;

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
    public void Parallel_CreateScope_ResolveScoped_Dispose()
    {
        var tasks = new Task[Threads];

        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < ScopesPerThread; i++)
                {
                    using var scope = _container.CreateScope();
                    _ = scope.ServiceProvider.GetService<IServiceB>();
                }
            });
        }

        Task.WaitAll(tasks);
    }
}

/// <summary>
/// Resolve N disposable transients into one scope. Each resolution registers a brand-new disposable, so
/// this exposes the O(n^2) <c>List.Contains</c> dedupe scan in DisposableContainer (A2 turns it into O(n)).
/// Fresh container per iteration so the disposable list starts empty.
/// </summary>
public class DisposableTransientResolutionBenchmarks
{
    [Params(100, 1000)]
    public int N;

    private ServiceContainer _container = null!;

    [IterationSetup]
    public void IterationSetup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IDisposableService, DisposableService>();
    }

    [IterationCleanup]
    public void IterationCleanup() => _container.Dispose();

    [Benchmark]
    public void Resolve_N_DisposableTransients()
    {
        for (int i = 0; i < N; i++)
            _ = _container.GetService<IDisposableService>();
    }
}

/// <summary>
/// First resolve of a graph in a fresh container. NOTE: the constructor-metadata/compiled-invoker cache is
/// process-static, so expression compilation is only paid on the very first iteration; this primarily tracks
/// per-container first-resolve overhead (descriptor population, scope setup). See results\README for the caveat.
/// </summary>
public class ColdResolveBenchmarks
{
    private ServiceContainer _container = null!;

    [IterationSetup]
    public void IterationSetup()
    {
        _container = new ServiceContainer();
        _container.RegisterTransient<IDep1, Dep1>();
        _container.RegisterTransient<IDep2, Dep2>();
        _container.RegisterTransient<IDep3, Dep3>();
        _container.RegisterTransient<IDep4, Dep4>();
        _container.RegisterTransient<IDep5, Dep5>();
        _container.RegisterTransient<IDep6, Dep6>();
        _container.RegisterTransient<IDep7, Dep7>();
        _container.RegisterTransient<IDep8, Dep8>();
        _container.RegisterTransient<IWideRoot, WideRoot>();
    }

    [IterationCleanup]
    public void IterationCleanup() => _container.Dispose();

    [Benchmark]
    public IWideRoot Cold_FirstResolve_WideGraph() => _container.GetService<IWideRoot>()!;
}
