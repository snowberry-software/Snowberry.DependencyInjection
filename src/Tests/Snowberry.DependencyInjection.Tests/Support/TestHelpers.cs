using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Support;

/// <summary>
/// Shared arrange and assert helpers for the container tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a container whose registrations can be overwritten and unregistered
    /// (<see cref="ServiceContainerOptions.Default"/> without <see cref="ServiceContainerOptions.ReadOnly"/>).
    /// </summary>
    /// <param name="additionalOptions">Options combined with the mutable defaults, for example <see cref="ServiceContainerOptions.ValidateScopes"/>.</param>
    /// <returns>A new mutable container.</returns>
    public static ServiceContainer CreateMutableContainer(ServiceContainerOptions additionalOptions = ServiceContainerOptions.None)
    {
        return new ServiceContainer((ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly) | additionalOptions);
    }

    /// <summary>
    /// Runs <paramref name="work"/> on <paramref name="workers"/> thread-pool tasks at once and fails the test
    /// with every exception any worker threw.
    /// </summary>
    /// <param name="workers">The number of concurrent workers.</param>
    /// <param name="work">The work of one worker; receives the worker index.</param>
    /// <returns>A task that completes when every worker has finished.</returns>
    /// <exception cref="AggregateException">One or more workers threw.</exception>
    public static async Task RunConcurrentlyAsync(int workers, Action<int> work)
    {
        var exceptions = new ConcurrentBag<Exception>();
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, workers).Select(index => Task.Run(() =>
        {
            // Release all workers together so they actually contend.
            start.Wait();
            try
            {
                work(index);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        if (!exceptions.IsEmpty)
            throw new AggregateException("One or more concurrent workers failed.", exceptions);
    }

    /// <summary>
    /// Creates <paramref name="count"/> scopes one after another, runs <paramref name="action"/> in each, and
    /// disposes each scope afterwards.
    /// </summary>
    /// <param name="container">The container that creates the scopes.</param>
    /// <param name="count">The number of scopes.</param>
    /// <param name="action">The work done in one scope; receives the scope and its index.</param>
    public static void ForEachScope(ServiceContainer container, int count, Action<IScope, int> action)
    {
        for (int i = 0; i < count; i++)
        {
            using var scope = container.CreateScope();
            action(scope, i);
        }
    }

    /// <summary>
    /// Asserts that no two items of <paramref name="items"/> are the same instance.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to check.</param>
    public static void AssertAllDistinct<T>(IReadOnlyCollection<T> items)
        where T : class
    {
        Assert.Equal(items.Count, items.Distinct(ReferenceComparer<T>.Instance).Count());
    }

    /// <summary>
    /// Compares items by reference.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        /// <summary>The shared instance.</summary>
        public static readonly ReferenceComparer<T> Instance = new();

        /// <inheritdoc/>
        public bool Equals(T? x, T? y)
        {
            return ReferenceEquals(x, y);
        }

        /// <inheritdoc/>
        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
