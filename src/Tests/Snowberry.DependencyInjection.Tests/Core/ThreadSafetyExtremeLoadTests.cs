using System.Collections.Concurrent;
using System.Diagnostics;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for thread safety under extreme load scenarios including concurrent registration,
/// resolution, scope creation/disposal, and race condition detection.
/// </summary>
public class ThreadSafetyExtremeLoadTests
{
    [Fact]
    public async Task ConcurrentServiceRegistration_ShouldBeSafe()
    {
        // Tests concurrent service registration from multiple threads

        // Arrange
        using var container = new ServiceContainer();
        const int threadCount = 20;
        const int registrationsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var registeredServices = new ConcurrentBag<string>();

        // Act
        var tasks = new List<Task>();
        for (int threadId = 0; threadId < threadCount; threadId++)
        {
            int capturedThreadId = threadId;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < registrationsPerThread; i++)
                    {
                        string serviceKey = $"thread_{capturedThreadId}_service_{i}";
                        container.RegisterSingleton<ITestService, TestService>(serviceKey);
                        registeredServices.Add(serviceKey);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Concurrent registration failed", exceptions);
        }

        Assert.Equal(threadCount * registrationsPerThread, registeredServices.Count);
        Assert.Equal(threadCount * registrationsPerThread, container.Count);
    }

    [Fact]
    public async Task ConcurrentServiceResolution_WithManyThreads_ShouldBeSafe()
    {
        // Tests concurrent service resolution under heavy load

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        const int threadCount = 50;
        const int resolutionsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var singletonInstances = new ConcurrentBag<ITestService>();
        var transientInstances = new ConcurrentBag<IDependentService>();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < resolutionsPerThread; j++)
                    {
                        var singleton = container.GetRequiredService<ITestService>();
                        var transient = container.GetRequiredService<IDependentService>();

                        if (singleton == null)
                            throw new InvalidOperationException("Singleton service was null");
                        if (transient == null)
                            throw new InvalidOperationException("Transient service was null");

                        singletonInstances.Add(singleton);
                        transientInstances.Add(transient);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Concurrent resolution failed", exceptions);
        }

        // All singleton instances should be the same
        var distinctSingletons = singletonInstances.Distinct().ToList();
        Assert.Single(distinctSingletons);

        // All transient instances should be different
        var distinctTransients = transientInstances.Distinct().ToList();
        Assert.Equal(threadCount * resolutionsPerThread, distinctTransients.Count);
    }

    [Fact]
    public async Task ConcurrentScopeCreationAndDisposal_ShouldBeSafe()
    {
        // Tests concurrent scope creation and disposal

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ITestService, TestService>();
        container.RegisterScoped<IDependentService, DependentService>();

        const int threadCount = 30;
        const int scopesPerThread = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var scopedServices = new ConcurrentBag<ITestService>();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < scopesPerThread; j++)
                    {
                        using var scope = container.CreateScope();
                        var service = scope.ServiceFactory.GetRequiredService<ITestService>();
                        var dependent = scope.ServiceFactory.GetRequiredService<IDependentService>();

                        if (service == null)
                            throw new InvalidOperationException("Scoped service was null");
                        if (dependent == null)
                            throw new InvalidOperationException("Dependent service was null");
                        if (dependent.PrimaryDependency == null)
                            throw new InvalidOperationException("Primary dependency was null");

                        scopedServices.Add(service);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Concurrent scope operations failed", exceptions);
        }

        Assert.Equal(threadCount * scopesPerThread, scopedServices.Count);

        // All scoped services should be disposed after their scopes ended
        Assert.All(scopedServices, service => Assert.True(service.IsDisposed));
    }

    [Fact]
    public async Task MixedConcurrentOperations_ShouldBeSafe()
    {
        // Tests mixed concurrent operations: registration, resolution, and scope operations

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        var exceptions = new ConcurrentBag<Exception>();
        int registrationCounter = 0;
        int resolutionCounter = 0;
        int scopeCounter = 0;

        // Act
        var tasks = new List<Task>();

        // Registration tasks
        for (int i = 0; i < 10; i++)
        {
            int capturedI = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 10; j++)
                    {
                        container.RegisterTransient<IDependentService, DependentService>($"key_{capturedI}_{j}");
                        Interlocked.Increment(ref registrationCounter);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Resolution tasks
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var service = container.GetRequiredService<ITestService>();
                        if (service == null)
                            throw new InvalidOperationException("Service was null");
                        Interlocked.Increment(ref resolutionCounter);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Scope operations
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 20; j++)
                    {
                        using var scope = container.CreateScope();
                        var service = scope.ServiceFactory.GetRequiredService<ITestService>();
                        if (service == null)
                            throw new InvalidOperationException("Scoped service was null");
                        Interlocked.Increment(ref scopeCounter);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Mixed concurrent operations failed", exceptions);
        }

        Assert.Equal(100, registrationCounter);
        Assert.Equal(1000, resolutionCounter);
        Assert.Equal(200, scopeCounter);
    }

    [Fact]
    public async Task ConcurrentServiceOverwrite_ShouldBeSafe()
    {
        // Tests concurrent service overwrite operations

        // Arrange
        using var container = new ServiceContainer();
        const int threadCount = 20;
        const int overwritesPerThread = 25;
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            int capturedI = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < overwritesPerThread; j++)
                    {
                        var instance = new TestService { Name = $"Thread_{capturedI}_Instance_{j}" };
                        container.RegisterSingleton<ITestService>(instance);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Concurrent service overwrite failed", exceptions);
        }

        Assert.Equal(1, container.Count); // Should still be 1 due to overwriting

        // Should be able to resolve the final service
        var finalService = container.GetRequiredService<ITestService>();
        Assert.NotNull(finalService);
    }

    [Fact]
    public async Task HighContentionSingletonAccess_ShouldBeSafe()
    {
        // Tests high contention access to singleton services

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) =>
        {
            // Simulate expensive singleton creation
            Thread.Sleep(1);
            return new TestService { Name = "ExpensiveSingleton" };
        });

        const int threadCount = 100;
        const int accessesPerThread = 10;
        var exceptions = new ConcurrentBag<Exception>();
        var instances = new ConcurrentBag<ITestService>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < accessesPerThread; j++)
                    {
                        var service = container.GetRequiredService<ITestService>();
                        if (service == null)
                            throw new InvalidOperationException("Service was null");
                        instances.Add(service);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("High contention singleton access failed", exceptions);
        }

        Assert.Equal(threadCount * accessesPerThread, instances.Count);

        // All instances should be the same (singleton behavior)
        var distinctInstances = instances.Distinct().ToList();
        Assert.Single(distinctInstances);
        Assert.Equal("ExpensiveSingleton", distinctInstances[0].Name);

        // Should complete reasonably quickly despite contention
        Assert.True(stopwatch.ElapsedMilliseconds < 5000);
    }

    [Fact]
    public async Task ConcurrentDisposal_ShouldBeSafe()
    {
        // Tests concurrent container disposal while operations are ongoing

        // Arrange
        var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();

        const int operationThreads = 10;
        var exceptions = new ConcurrentBag<Exception>();
        var disposedExceptions = new ConcurrentBag<ObjectDisposedException>();
        int successfulOperations = 0;

        // Act
        var operationTasks = new List<Task>();

        // Start operation threads
        for (int i = 0; i < operationThreads; i++)
        {
            operationTasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        try
                        {
                            var service = container.GetRequiredService<ITestService>();
                            if (service != null)
                            {
                                Interlocked.Increment(ref successfulOperations);
                            }
                        }
                        catch (ObjectDisposedException ex)
                        {
                            disposedExceptions.Add(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Dispose container after short delay
        var disposalTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            container.Dispose();
        });

        await Task.WhenAll(operationTasks.Concat(new[] { disposalTask }));

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Concurrent disposal operations failed", exceptions);
        }

        Assert.True(successfulOperations > 0); // Some operations should succeed before disposal

        // ObjectDisposedExceptions are expected after disposal
        if (disposedExceptions.Count > 0)
        {
            Assert.All(disposedExceptions, ex => Assert.Contains("ServiceContainer", ex.ObjectName));
        }
    }

    [Fact]
    public async Task StressTest_MassiveParallelLoad_ShouldRemainStable()
    {
        // Extreme stress test with massive parallel load

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterTransient<IDependentService, DependentService>();
        container.RegisterScoped<IComplexService, ComplexService>();

        int threadCount = Math.Max(Environment.ProcessorCount * 2, 8); // Scale with CPU cores but not too extreme
        const int operationsPerThread = 100; // Reduced for stability
        var exceptions = new ConcurrentBag<Exception>();
        int totalOperations = 0;
        long memoryBefore = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var random = new Random(Thread.CurrentThread.ManagedThreadId); // Seed with thread ID for reproducibility

                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        switch (random.Next(4))
                        {
                            case 0: // Singleton access
                                var singleton = container.GetRequiredService<ITestService>();
                                if (singleton == null)
                                    throw new InvalidOperationException("Singleton was null");
                                break;

                            case 1: // Transient access
                                var transient = container.GetRequiredService<IDependentService>();
                                if (transient == null)
                                    throw new InvalidOperationException("Transient was null");
                                break;

                            case 2: // Scoped access
                                using (var scope = container.CreateScope())
                                {
                                    var scoped = scope.ServiceFactory.GetRequiredService<IComplexService>();
                                    if (scoped == null)
                                        throw new InvalidOperationException("Scoped was null");
                                }

                                break;

                            case 3: // Service registration
                                string key = $"dynamic_{Thread.CurrentThread.ManagedThreadId}_{j}";
                                container.RegisterTransient<ITestService, AlternativeTestService>(key);
                                break;
                        }

                        Interlocked.Increment(ref totalOperations);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        long memoryAfter = GC.GetTotalMemory(true);
        long memoryUsed = memoryAfter - memoryBefore;

        // Assert
        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("Massive parallel load test failed", exceptions);
        }

        Assert.Equal(threadCount * operationsPerThread, totalOperations);

        // Performance assertions
        Assert.True(stopwatch.ElapsedMilliseconds < 30000); // Should complete within 30 seconds
        Assert.True(memoryUsed < 100 * 1024 * 1024); // Should use less than 100MB additional memory

        // Container should still be functional
        var finalService = container.GetRequiredService<ITestService>();
        Assert.NotNull(finalService);
    }
}