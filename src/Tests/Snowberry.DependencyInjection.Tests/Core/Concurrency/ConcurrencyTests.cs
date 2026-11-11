using System.Collections.Concurrent;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.Support;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Concurrency;

/// <summary>
/// Tests for thread safety under concurrent load: registration, resolution, scope creation and disposal,
/// container disposal races, and the per-scope scoped resolution design (scoped instances are constructed
/// outside any lock and published under the per-scope lock, so cross-lifetime dependencies cannot invert
/// lock order and a scoped service is observed as a single instance per scope).
/// </summary>
public class ConcurrencyTests
{
    /// <summary>Upper bound for the deadlock guards; far above the real runtime of the guarded work.</summary>
    private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(60);

    /// <summary>A singleton that depends on a scoped service.</summary>
    private interface IRootSingleton
    {
        /// <summary>Gets the scoped dependency.</summary>
        IScopedDep Dep { get; }
    }

    /// <summary>A scoped service consumed by a singleton.</summary>
    private interface IScopedDep;

    /// <summary>Default <see cref="IScopedDep"/> implementation.</summary>
    private sealed class ScopedDep : IScopedDep;

    /// <summary>Singleton that depends on a scoped service.</summary>
    /// <param name="dep">The scoped dependency.</param>
    private sealed class RootSingleton(IScopedDep dep) : IRootSingleton
    {
        /// <inheritdoc/>
        public IScopedDep Dep { get; } = dep;
    }

    /// <summary>A singleton consumed by a scoped service.</summary>
    private interface ILeafSingleton;

    /// <summary>Default <see cref="ILeafSingleton"/> implementation.</summary>
    private sealed class LeafSingleton : ILeafSingleton;

    /// <summary>A scoped service that depends on a singleton.</summary>
    private interface IScopedConsumer
    {
        /// <summary>Gets the singleton dependency.</summary>
        ILeafSingleton Leaf { get; }
    }

    /// <summary>Scoped service that depends on a singleton.</summary>
    /// <param name="leaf">The singleton dependency.</param>
    private sealed class ScopedConsumer(ILeafSingleton leaf) : IScopedConsumer
    {
        /// <inheritdoc/>
        public ILeafSingleton Leaf { get; } = leaf;
    }

    /// <summary>
    /// Builds a container with a singleton-to-scoped chain and a scoped-to-singleton chain.
    /// </summary>
    /// <returns>The configured container.</returns>
    private static ServiceContainer BuildCrossLifetimeContainer()
    {
        var container = new ServiceContainer();
        container.RegisterSingleton<IRootSingleton, RootSingleton>(); // singleton -> scoped
        container.RegisterScoped<IScopedDep, ScopedDep>();
        container.RegisterScoped<IScopedConsumer, ScopedConsumer>(); // scoped -> singleton
        container.RegisterSingleton<ILeafSingleton, LeafSingleton>();
        return container;
    }

    /// <summary>
    /// Fails the test when <paramref name="work"/> does not finish within <see cref="DeadlockTimeout"/>.
    /// </summary>
    /// <param name="work">The concurrent work to observe.</param>
    /// <param name="description">The scenario named in the failure message.</param>
    /// <returns>A task that completes when the work has finished and propagates its failures.</returns>
    private static async Task AssertCompletesAsync(Task work, string description)
    {
        var finished = await Task.WhenAny(work, Task.Delay(DeadlockTimeout));

        Assert.True(ReferenceEquals(finished, work), $"{description} did not complete (deadlock/timeout).");
        await work;
    }

    [Fact]
    public async Task ConcurrentServiceRegistration_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        const int threadCount = 8;
        const int registrationsPerThread = 100;

        // Act
        await TestHelpers.RunConcurrentlyAsync(threadCount, threadId =>
        {
            for (int i = 0; i < registrationsPerThread; i++)
            {
                container.RegisterSingleton<ITestService, TestService>($"thread_{threadId}_service_{i}");
            }
        });

        // Assert
        Assert.Equal(threadCount * registrationsPerThread, container.Count);
    }

    [Fact]
    public async Task ConcurrentServiceResolution_WithManyThreads_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        const int threadCount = 8;
        const int resolutionsPerThread = 200;
        var singletonInstances = new ConcurrentBag<ITestService>();
        var transientInstances = new ConcurrentBag<IDependentService>();

        // Act
        await TestHelpers.RunConcurrentlyAsync(threadCount, _ =>
        {
            for (int j = 0; j < resolutionsPerThread; j++)
            {
                singletonInstances.Add(container.GetRequiredService<ITestService>());
                transientInstances.Add(container.GetRequiredService<IDependentService>());
            }
        });

        // Assert
        Assert.Single(singletonInstances.Distinct());
        Assert.Equal(threadCount * resolutionsPerThread, transientInstances.Count);
        TestHelpers.AssertAllDistinct(transientInstances);
    }

    [Fact]
    public async Task ConcurrentScopeCreationAndDisposal_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();

        const int threadCount = 8;
        const int scopesPerThread = 50;
        var scopedServices = new ConcurrentBag<ITestService>();

        // Act
        await TestHelpers.RunConcurrentlyAsync(threadCount, _ =>
        {
            TestHelpers.ForEachScope(container, scopesPerThread, (scope, _) =>
            {
                scopedServices.Add(scope.ServiceProvider.GetRequiredService<ITestService>());
                var dependent = scope.ServiceProvider.GetRequiredService<IDependentService>();
                Assert.NotNull(dependent.PrimaryDependency);
            });
        });

        // Assert
        Assert.Equal(threadCount * scopesPerThread, scopedServices.Count);
        TestHelpers.AssertAllDistinct(scopedServices);

        // All scoped services should be disposed after their scopes ended
        Assert.All(scopedServices, service => Assert.True(service.IsDisposed));
    }

    [Fact]
    public async Task MixedConcurrentOperations_ShouldBeSafe()
    {
        // Registration, resolution, and scope workers run against the same container at once.

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        const int workersPerKind = 4;
        const int iterations = 25;
        int registrationCounter = 0;
        int resolutionCounter = 0;
        int scopeCounter = 0;

        // Act
        await TestHelpers.RunConcurrentlyAsync(workersPerKind * 3, worker =>
        {
            for (int j = 0; j < iterations; j++)
            {
                switch (worker % 3)
                {
                    case 0:
                        container.RegisterTransient<IDependentService, DependentService>($"key_{worker}_{j}");
                        Interlocked.Increment(ref registrationCounter);
                        break;
                    case 1:
                        container.GetRequiredService<ITestService>();
                        Interlocked.Increment(ref resolutionCounter);
                        break;
                    default:
                        using (var scope = container.CreateScope())
                        {
                            scope.ServiceProvider.GetRequiredService<ITestService>();
                        }

                        Interlocked.Increment(ref scopeCounter);
                        break;
                }
            }
        });

        // Assert
        Assert.Equal(workersPerKind * iterations, registrationCounter);
        Assert.Equal(workersPerKind * iterations, resolutionCounter);
        Assert.Equal(workersPerKind * iterations, scopeCounter);
        Assert.Equal(1 + (workersPerKind * iterations), container.Count);
    }

    [Fact]
    public async Task ConcurrentServiceOverwrite_ShouldBeSafe()
    {
        // Arrange
        using var container = TestHelpers.CreateMutableContainer();
        const int threadCount = 8;
        const int overwritesPerThread = 25;
        var registered = new ConcurrentBag<TestService>();

        // Act
        await TestHelpers.RunConcurrentlyAsync(threadCount, threadId =>
        {
            for (int j = 0; j < overwritesPerThread; j++)
            {
                var instance = new TestService { Name = $"Thread_{threadId}_Instance_{j}" };
                registered.Add(instance);
                container.RegisterSingleton<ITestService>(instance);
            }
        });

        // Assert
        Assert.Equal(1, container.Count); // Should still be 1 due to overwriting
        Assert.Equal(threadCount * overwritesPerThread, registered.Count);

        // The surviving registration must be one of the instances that was registered
        var finalService = container.GetRequiredService<ITestService>();
        Assert.Contains(registered, candidate => ReferenceEquals(candidate, finalService));
    }

    [Fact]
    public async Task HighContentionSingletonAccess_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) => new TestService { Name = "ContendedSingleton" });

        const int threadCount = 16;
        const int accessesPerThread = 10;
        var instances = new ConcurrentBag<ITestService>();

        // Act
        await TestHelpers.RunConcurrentlyAsync(threadCount, _ =>
        {
            for (int j = 0; j < accessesPerThread; j++)
            {
                instances.Add(container.GetRequiredService<ITestService>());
            }
        });

        // Assert
        Assert.Equal(threadCount * accessesPerThread, instances.Count);

        // All instances should be the same (singleton behavior)
        var distinctInstance = Assert.Single(instances.Distinct());
        Assert.Equal("ContendedSingleton", distinctInstance.Name);
    }

    [Fact]
    public async Task ConcurrentResolution_ThenDisposal_ShouldRejectEveryLaterResolve()
    {
        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        await TestHelpers.RunConcurrentlyAsync(8, _ =>
        {
            for (int j = 0; j < 100; j++)
            {
                container.GetRequiredService<ITestService>();
            }
        });

        // Act
        container.Dispose();

        // Assert: every later resolve, from any thread, is rejected with ObjectDisposedException
        await TestHelpers.RunConcurrentlyAsync(8, _ =>
        {
            Assert.Throws<ObjectDisposedException>(container.GetRequiredService<ITestService>);
            Assert.Throws<ObjectDisposedException>(container.GetRequiredService<IDependentService>);
        });
    }

    [Fact]
    public async Task ConcurrentDisposal_WhileResolving_ShouldOnlyFailWithObjectDisposedException()
    {
        // Every worker first resolves successfully (all signal a countdown); only then does worker 0 dispose the
        // container while the others are still in their loops. Any exception other than ObjectDisposedException
        // escapes the worker and fails the test through RunConcurrentlyAsync.

        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        const int workers = 8;
        using var allResolvedOnce = new CountdownEvent(workers);
        int successfulResolves = 0;

        // Act
        await TestHelpers.RunConcurrentlyAsync(workers, worker =>
        {
            container.GetRequiredService<ITestService>();
            Interlocked.Increment(ref successfulResolves);
            allResolvedOnce.Signal();

            if (worker == 0)
            {
                allResolvedOnce.Wait();
                container.Dispose();
                return;
            }

            for (int j = 0; j < 1000; j++)
            {
                try
                {
                    container.GetRequiredService<ITestService>();
                    Interlocked.Increment(ref successfulResolves);
                }
                catch (ObjectDisposedException)
                {
                    // Expected once the container has been disposed.
                }
            }
        });

        // Assert
        Assert.True(successfulResolves >= workers);
        Assert.True(container.IsDisposed);
        Assert.Throws<ObjectDisposedException>(container.GetRequiredService<ITestService>);
    }

    [Fact]
    public async Task CrossLifetimeDependencies_ResolvedConcurrentlyAcrossScopes_DoNotDeadlock()
    {
        // A singleton depending on a scoped service and a scoped service depending on a singleton, resolved
        // concurrently across many independent scopes, must never invert lock order. Pre-B2 the container-wide
        // lock around scoped creation could deadlock against the singleton init lock.
        using var container = BuildCrossLifetimeContainer();

        const int threadCount = 16;
        const int iterationsPerThread = 100;
        var rootSingletons = new ConcurrentBag<IRootSingleton>();
        var leafSingletons = new ConcurrentBag<ILeafSingleton>();

        var work = TestHelpers.RunConcurrentlyAsync(threadCount, _ =>
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
        });

        await AssertCompletesAsync(work, "Concurrent cross-lifetime resolution");

        // Singletons are container-wide single instances regardless of resolving thread.
        Assert.Single(rootSingletons.Distinct());
        Assert.Single(leafSingletons.Distinct());
    }

    [Fact]
    public async Task SameScopedService_ResolvedConcurrentlyInOneScope_YieldsExactlyOneInstance()
    {
        // Many threads racing to first-resolve the SAME scoped service in the SAME scope must all observe a
        // single instance (the construct-outside-lock + double-checked publish must discard losers).
        using var container = BuildCrossLifetimeContainer();
        using var scope = container.CreateScope();

        const int threadCount = 32;
        var resolved = new ConcurrentBag<IScopedConsumer>();

        var work = TestHelpers.RunConcurrentlyAsync(threadCount, _ =>
            resolved.Add(scope.ServiceProvider.GetRequiredService<IScopedConsumer>()));

        await AssertCompletesAsync(work, "Concurrent same-scope resolution");

        Assert.Equal(threadCount, resolved.Count);
        Assert.Single(resolved.Distinct()); // exactly one instance observed by every thread
    }
}
