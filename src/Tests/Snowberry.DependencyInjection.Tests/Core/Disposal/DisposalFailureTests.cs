using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Disposal;

/// <summary>
/// Tests that disposal visits every tracked instance even when some of them fail, and how the failures are
/// reported: a single failure is rethrown unchanged, several are thrown as an <see cref="AggregateException"/>.
/// </summary>
public class DisposalFailureTests
{
    /// <summary>
    /// Resolves the <see cref="RecordingDisposable"/> registered under <paramref name="key"/>.
    /// </summary>
    /// <param name="container">The container to resolve from.</param>
    /// <param name="key">The service key.</param>
    /// <returns>The resolved instance.</returns>
    private static RecordingDisposable Resolve(ServiceContainer container, string key)
    {
        return container.GetRequiredKeyedService<RecordingDisposable>(key);
    }

    /// <summary>
    /// Registers a transient <see cref="RecordingDisposable"/> named and keyed <paramref name="key"/>.
    /// </summary>
    /// <param name="container">The container to register in.</param>
    /// <param name="key">The service key, also used as the recorded name.</param>
    /// <param name="log">The log the instance records its disposal in.</param>
    /// <param name="failure">The exception thrown on disposal, or <see langword="null"/> to dispose successfully.</param>
    private static void RegisterRecording(ServiceContainer container, string key, DisposalLog log, Exception? failure = null)
    {
        container.RegisterTransient<RecordingDisposable>((_, _) => new RecordingDisposable(key, log, failure), key);
    }

    [Fact]
    public void Dispose_WhenOneServiceThrows_DisposesAllOthersAndRethrowsTheOriginalException()
    {
        var log = new DisposalLog();
        var failure = new InvalidOperationException("boom");
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log);
        RegisterRecording(container, "b", log, failure);
        RegisterRecording(container, "c", log);

        var a = Resolve(container, "a");
        var b = Resolve(container, "b");
        var c = Resolve(container, "c");

        var thrown = Assert.Throws<InvalidOperationException>(container.Dispose);

        Assert.Same(failure, thrown);
        Assert.True(a.IsDisposed);
        Assert.True(b.IsDisposed);
        Assert.True(c.IsDisposed);
        Assert.Equal(["c", "b", "a"], log.Entries);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public void Dispose_WhenSeveralServicesThrow_ThrowsAggregateExceptionInDisposalOrder()
    {
        var log = new DisposalLog();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log, second);
        RegisterRecording(container, "b", log);
        RegisterRecording(container, "c", log, first);

        _ = Resolve(container, "a");
        _ = Resolve(container, "b");
        _ = Resolve(container, "c");

        var thrown = Assert.Throws<AggregateException>(container.Dispose);

        Assert.Equal([first, second], thrown.InnerExceptions);
        Assert.Equal(["c", "b", "a"], log.Entries);
    }

    [Fact]
    public void Dispose_WhenServiceThrows_SecondDisposeIsANoOp()
    {
        var log = new DisposalLog();
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log, new InvalidOperationException("boom"));
        _ = Resolve(container, "a");

        Assert.Throws<InvalidOperationException>(container.Dispose);
        container.Dispose();

        Assert.Equal(["a"], log.Entries);
    }

    [Fact]
    public void ScopeDispose_WhenScopedServiceThrows_DisposesOthersAndRaisesOnDispose()
    {
        var log = new DisposalLog();
        var failure = new InvalidOperationException("boom");
        var container = new ServiceContainer();
        container.RegisterScoped<RecordingDisposable>((_, _) => new RecordingDisposable("scoped-a", log), "a");
        container.RegisterScoped<RecordingDisposable>((_, _) => new RecordingDisposable("scoped-b", log, failure), "b");

        var scope = container.CreateScope();
        bool onDisposeRaised = false;
        scope.OnDispose += (_, _) => onDisposeRaised = true;

        var a = scope.ServiceProvider.GetRequiredKeyedService<RecordingDisposable>("a");
        _ = scope.ServiceProvider.GetRequiredKeyedService<RecordingDisposable>("b");

        var thrown = Assert.Throws<InvalidOperationException>(scope.Dispose);

        Assert.Same(failure, thrown);
        Assert.True(a.IsDisposed);
        Assert.True(onDisposeRaised);
        Assert.True(scope.IsDisposed);
        Assert.Equal(["scoped-b", "scoped-a"], log.Entries);
    }

    [Fact]
    public void RegisterDisposable_AfterDisposal_ThrowsObjectDisposedException()
    {
        var log = new DisposalLog();
        var container = new ServiceContainer();
        var disposableContainer = container.DisposableContainer;
        container.Dispose();

        var late = new RecordingDisposable("late", log);

        Assert.Throws<ObjectDisposedException>(() => disposableContainer.RegisterDisposable(late));
        Assert.False(late.IsDisposed);
    }

    [Fact]
    public void RemoveDisposable_AfterDisposal_DoesNotChangeTrackedCount()
    {
        var log = new DisposalLog();
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log);
        var a = Resolve(container, "a");

        container.Dispose();
        container.DisposableContainer.RemoveDisposable(a);

        Assert.Equal(1, container.DisposableContainer.DisposableCount);
    }

#if NETCOREAPP
    [Fact]
    public void Dispose_WithAsyncOnlyService_ThrowsInvalidOperationButStillDisposesOthers()
    {
        var log = new DisposalLog();
        var container = new ServiceContainer();
        RegisterRecording(container, "sync", log);
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("async", log), "async");

        var sync = Resolve(container, "sync");
        var asyncOnly = container.GetRequiredKeyedService<RecordingAsyncDisposable>("async");

        Assert.Throws<InvalidOperationException>(container.Dispose);

        Assert.True(sync.IsDisposed);
        Assert.False(asyncOnly.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WhenOneServiceFailsAfterYielding_DisposesAllOthersAndRethrowsTheOriginalException()
    {
        var log = new DisposalLog();
        var failure = new InvalidOperationException("boom");
        var container = new ServiceContainer();
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("a", log), "a");
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("b", log, failure, failAfterYield: true), "b");
        RegisterRecording(container, "c", log);

        var a = container.GetRequiredKeyedService<RecordingAsyncDisposable>("a");
        _ = container.GetRequiredKeyedService<RecordingAsyncDisposable>("b");
        var c = Resolve(container, "c");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => container.DisposeAsync().AsTask());

        Assert.Same(failure, thrown);
        Assert.True(a.IsDisposed);
        Assert.True(c.IsDisposed);
        Assert.Equal(["c", "b", "a"], log.Entries);
    }

    [Fact]
    public async Task DisposeAsync_WhenSeveralServicesFail_ThrowsAggregateExceptionInDisposalOrder()
    {
        var log = new DisposalLog();
        var syncFailure = new InvalidOperationException("sync");
        var yieldedFailure = new ArgumentException("yielded");
        var plainFailure = new NotSupportedException("plain");
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log, plainFailure);
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("b", log, yieldedFailure, failAfterYield: true), "b");
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("c", log, syncFailure), "c");

        _ = Resolve(container, "a");
        _ = container.GetRequiredKeyedService<RecordingAsyncDisposable>("b");
        _ = container.GetRequiredKeyedService<RecordingAsyncDisposable>("c");

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => container.DisposeAsync().AsTask());

        Assert.Equal([syncFailure, yieldedFailure, plainFailure], thrown.InnerExceptions);
        Assert.Equal(["c", "b", "a"], log.Entries);
    }

    [Fact]
    public async Task DisposeAsync_WhenSynchronousFailuresOnly_ThrowsAggregateException()
    {
        var log = new DisposalLog();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var container = new ServiceContainer();
        RegisterRecording(container, "a", log, second);
        container.RegisterTransient<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("b", log, first), "b");

        _ = Resolve(container, "a");
        _ = container.GetRequiredKeyedService<RecordingAsyncDisposable>("b");

        var thrown = await Assert.ThrowsAsync<AggregateException>(() => container.DisposeAsync().AsTask());

        Assert.Equal([first, second], thrown.InnerExceptions);
    }

    [Fact]
    public async Task ScopeDisposeAsync_WhenScopedServiceFails_DisposesOthersAndRaisesOnDispose()
    {
        var log = new DisposalLog();
        var failure = new InvalidOperationException("boom");
        var container = new ServiceContainer();
        container.RegisterScoped<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("scoped-a", log), "a");
        container.RegisterScoped<RecordingAsyncDisposable>((_, _) => new RecordingAsyncDisposable("scoped-b", log, failure, failAfterYield: true), "b");

        var scope = container.CreateScope();
        bool onDisposeRaised = false;
        scope.OnDispose += (_, _) => onDisposeRaised = true;

        var a = scope.ServiceProvider.GetRequiredKeyedService<RecordingAsyncDisposable>("a");
        _ = scope.ServiceProvider.GetRequiredKeyedService<RecordingAsyncDisposable>("b");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());

        Assert.Same(failure, thrown);
        Assert.True(a.IsDisposed);
        Assert.True(onDisposeRaised);
        Assert.Equal(["scoped-b", "scoped-a"], log.Entries);
    }
#endif
}
