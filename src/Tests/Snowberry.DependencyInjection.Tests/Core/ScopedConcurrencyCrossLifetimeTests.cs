using System.Collections.Concurrent;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Concurrency tests for the per-scope scoped resolution design (B2): scoped instances are constructed
/// outside any lock and published under the per-scope lock, so cross-lifetime dependencies cannot invert
/// lock order (no deadlock) and a scoped service is observed as a single instance per scope.
/// </summary>
public class ScopedConcurrencyCrossLifetimeTests
{
    // Chain 1: a singleton that depends on a scoped service.
    private interface IRootSingleton
    {
        IScopedDep Dep { get; }
    }

    private interface IScopedDep;

    private sealed class ScopedDep : IScopedDep;

    private sealed class RootSingleton(IScopedDep dep) : IRootSingleton
    {
        public IScopedDep Dep { get; } = dep;
    }

    // Chain 2: a scoped service that depends on a singleton.
    private interface ILeafSingleton;

    private sealed class LeafSingleton : ILeafSingleton;

    private interface IScopedConsumer
    {
        ILeafSingleton Leaf { get; }
    }

    private sealed class ScopedConsumer(ILeafSingleton leaf) : IScopedConsumer
    {
        public ILeafSingleton Leaf { get; } = leaf;
    }

    private static ServiceContainer BuildContainer()
    {
        var container = new ServiceContainer();
        container.RegisterSingleton<IRootSingleton, RootSingleton>(); // singleton -> scoped
        container.RegisterScoped<IScopedDep, ScopedDep>();
        container.RegisterScoped<IScopedConsumer, ScopedConsumer>(); // scoped -> singleton
        container.RegisterSingleton<ILeafSingleton, LeafSingleton>();
        return container;
    }

    [Fact]
    public async Task CrossLifetimeDependencies_ResolvedConcurrentlyAcrossScopes_DoNotDeadlock()
    {
        // A singleton depending on a scoped service and a scoped service depending on a singleton, resolved
        // concurrently across many independent scopes, must never invert lock order. Pre-B2 the container-wide
        // lock around scoped creation could deadlock against the singleton init lock.
        using var container = BuildContainer();

        const int threadCount = 32;
        const int iterationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var rootSingletons = new ConcurrentBag<IRootSingleton>();
        var leafSingletons = new ConcurrentBag<ILeafSingleton>();

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        // Singleton-that-depends-on-scoped, resolved off the root provider.
                        rootSingletons.Add(container.GetRequiredService<IRootSingleton>());

                        // Scoped-that-depends-on-singleton, resolved inside a fresh scope.
                        using var scope = container.CreateScope();
                        var consumer = scope.ServiceProvider.GetRequiredService<IScopedConsumer>();
                        leafSingletons.Add(consumer.Leaf);

                        // Same scoped service resolved again in the same scope must be the same instance.
                        Assert.Same(consumer, scope.ServiceProvider.GetRequiredService<IScopedConsumer>());
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        var all = Task.WhenAll(tasks);
        var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(ReferenceEquals(finished, all), "Concurrent cross-lifetime resolution did not complete (deadlock/timeout).");
        await all;

        Assert.True(exceptions.IsEmpty, exceptions.IsEmpty ? string.Empty : string.Join("; ", exceptions.Select(e => e.Message)));

        // Singletons are process/container-wide single instances regardless of resolving thread.
        Assert.Single(rootSingletons.Distinct());
        Assert.Single(leafSingletons.Distinct());
    }

    [Fact]
    public async Task SameScopedService_ResolvedConcurrentlyInOneScope_YieldsExactlyOneInstance()
    {
        // Many threads racing to first-resolve the SAME scoped service in the SAME scope must all observe a
        // single instance (the construct-outside-lock + double-checked publish must discard losers).
        using var container = BuildContainer();
        using var scope = container.CreateScope();

        const int threadCount = 64;
        var resolved = new ConcurrentBag<IScopedConsumer>();
        var exceptions = new ConcurrentBag<Exception>();
        using var start = new ManualResetEventSlim(false);

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    start.Wait();
                    resolved.Add(scope.ServiceProvider.GetRequiredService<IScopedConsumer>());
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        start.Set(); // release all threads at once to maximize the race window

        var all = Task.WhenAll(tasks);
        var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.True(ReferenceEquals(finished, all), "Concurrent same-scope resolution did not complete (deadlock/timeout).");
        await all;

        Assert.True(exceptions.IsEmpty, exceptions.IsEmpty ? string.Empty : string.Join("; ", exceptions.Select(e => e.Message)));
        Assert.Equal(threadCount, resolved.Count);
        Assert.Single(resolved.Distinct()); // exactly one instance observed by every thread
    }
}
