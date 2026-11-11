using System.Runtime.ExceptionServices;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;

namespace Snowberry.DependencyInjection;

/// <inheritdoc cref="IDisposableContainer"/>
/// <remarks>
/// Disposal runs in reverse registration order and always visits every tracked instance: a failing instance does
/// not stop the remaining ones from being disposed. A single failure is rethrown unchanged; several failures are
/// thrown together as an <see cref="AggregateException"/> in disposal order.
/// </remarks>
internal class DisposableContainer : IDisposableContainer
{
    private List<object>? _disposables;
    private bool _isDisposed;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    /// <summary>
    /// Creates a container that is already disposed and tracks nothing. It rejects every registration and never
    /// changes state, so one instance can be shared.
    /// </summary>
    /// <returns>A disposed, empty container.</returns>
    internal static DisposableContainer CreateDisposed()
    {
        return new DisposableContainer { _isDisposed = true };
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The container has already been disposed.</exception>
    public void RegisterDisposable(IDisposable disposable)
    {
        RegisterDisposable((object)disposable);
    }

    /// <inheritdoc/>
    /// <remarks>Has no effect once the container has been disposed.</remarks>
    public void RemoveDisposable(object? instance)
    {
        if (instance == null)
            return;

        lock (_lock)
        {
            // Once disposal has started the tracked list is being drained outside the lock, so it must not change.
            if (_isDisposed)
                return;

            _disposables?.Remove(instance);
        }
    }

    /// <inheritdoc/>
    public void RemoveDisposable(IDisposable disposable)
    {
        RemoveDisposable((object)disposable);
    }

#if NETCOREAPP
    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The container has already been disposed.</exception>
    public void RegisterDisposable(IAsyncDisposable disposable)
    {
        RegisterDisposable((object)disposable);
    }

    /// <inheritdoc/>
    public void RemoveDisposable(IAsyncDisposable disposable)
    {
        RemoveDisposable((object)disposable);
    }
#endif

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="disposable"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="disposable"/> implements neither <see cref="IDisposable"/> nor <c>IAsyncDisposable</c>.</exception>
    /// <exception cref="ObjectDisposedException">The container has already been disposed.</exception>
    public void RegisterDisposable(object disposable)
    {
        _ = disposable ?? throw new ArgumentNullException(nameof(disposable));

        bool hasAsyncDisposable = false;
        bool hasDisposable = false;

#if NETCOREAPP
        if (disposable is IAsyncDisposable)
            hasAsyncDisposable = true;
#endif

        if (disposable is IDisposable)
            hasDisposable = true;

        if (!hasAsyncDisposable && !hasDisposable)
            throw new InvalidOperationException($"`{disposable.GetType().FullName}` type does not implement IAsyncDisposable or IDisposable.");

        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

            if (_disposables == null)
            {
                _disposables = [disposable];
                return;
            }

            if (_disposables.Contains(disposable))
                return;

            _disposables.Add(disposable);
        }
    }

    /// <summary>
    /// Tracks a newly created instance without the duplicate and type checks of <see cref="RegisterDisposable(object)"/>.
    /// The caller guarantees that <paramref name="disposable"/> implements <see cref="IDisposable"/> or
    /// <c>IAsyncDisposable</c> and is not tracked yet.
    /// </summary>
    /// <param name="disposable">The freshly created instance to track.</param>
    /// <exception cref="ObjectDisposedException">The container has already been disposed; <paramref name="disposable"/> is disposed before the exception is thrown.</exception>
    internal void AddDisposableUnchecked(object disposable)
    {
        // The create-and-track path always passes a fresh instance, so skipping the List.Contains dedupe keeps
        // tracking N services O(n) instead of O(n^2).
        lock (_lock)
        {
            if (!_isDisposed)
            {
                (_disposables ??= []).Add(disposable);
                return;
            }
        }

        // Lost a race with disposal: nobody will drain this instance any more, so dispose it now and report the
        // disposed container to the resolving caller.
        DisposeLate(disposable);
        DisposeThrowHelper.ThrowIfDisposed(true, this);
    }

    /// <summary>
    /// Synchronously disposes an instance that was created after the container was disposed.
    /// </summary>
    /// <param name="disposable">The instance to dispose.</param>
    private static void DisposeLate(object disposable)
    {
        if (disposable is IDisposable syncDisposable)
        {
            syncDisposable.Dispose();
            return;
        }

#if NETCOREAPP
        if (disposable is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
#endif
    }

    /// <summary>
    /// Marks the container as disposed and returns the instances to dispose.
    /// </summary>
    /// <returns>The tracked instances, or <see langword="null"/> when the container was already disposed or tracks nothing.</returns>
    // After _isDisposed is set every mutation of _disposables is rejected under the lock, so the returned list can
    // be drained outside the lock without copying. User Dispose code therefore never runs while the lock is held.
    private List<object>? BeginDispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
                return null;

            _isDisposed = true;
            return _disposables is { Count: > 0 } disposables ? disposables : null;
        }
    }

    /// <summary>
    /// Throws the collected disposal failures: the single exception unchanged, or an <see cref="AggregateException"/> for several.
    /// </summary>
    /// <param name="exceptions">The collected failures, or <see langword="null"/> when disposal succeeded.</param>
    private static void ThrowIfFailed(List<Exception>? exceptions)
    {
        if (exceptions == null)
            return;

        if (exceptions.Count == 1)
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();

        throw new AggregateException(exceptions);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">A tracked instance only implements <c>IAsyncDisposable</c>; use <c>DisposeAsync</c> instead. The remaining instances are still disposed.</exception>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose.</exception>
    public void Dispose()
    {
        var disposables = BeginDispose();
        if (disposables == null)
            return;

        List<Exception>? exceptions = null;

        for (int i = disposables.Count - 1; i >= 0; i--)
        {
            object disposableObj = disposables[i];

            try
            {
                if (disposableObj is IDisposable disposable)
                    disposable.Dispose();
                else
                    throw new InvalidOperationException($"`{disposableObj.GetType().FullName}` type only implements IAsyncDisposable. Use `DisposeAsync` to dispose the container.");
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        ThrowIfFailed(exceptions);
    }

#if NETCOREAPP
    /// <inheritdoc/>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose.</exception>
    public ValueTask DisposeAsync()
    {
        var disposables = BeginDispose();
        if (disposables == null)
            return default;

        List<Exception>? exceptions = null;

        for (int i = disposables.Count - 1; i >= 0; i--)
        {
            object disposable = disposables[i];

            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                {
                    var vt = asyncDisposable.DisposeAsync();

                    if (!vt.IsCompletedSuccessfully)
                        return Await(i, vt, disposables, exceptions);

                    // Completes the (possibly IValueTaskSource-backed) ValueTask so its source can be reused.
                    vt.GetAwaiter().GetResult();
                    continue;
                }

                ((IDisposable)disposable).Dispose();
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions == null)
            return default;

        return new ValueTask(Task.FromException(exceptions.Count == 1 ? exceptions[0] : new AggregateException(exceptions)));

        static async ValueTask Await(int i, ValueTask pending, List<object> toDispose, List<Exception>? exceptions)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }

            // NOTE(VNC): Go to next element.
            for (i--; i >= 0; i--)
            {
                object disposable = toDispose[i];

                try
                {
                    if (disposable is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else
                        ((IDisposable)disposable).Dispose();
                }
                catch (Exception exception)
                {
                    (exceptions ??= []).Add(exception);
                }
            }

            ThrowIfFailed(exceptions);
        }
    }
#endif

    /// <inheritdoc/>
    public int DisposableCount
    {
        get
        {
            lock (_lock)
            {
                return _disposables?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the container has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock)
                return _isDisposed;
        }
    }
}
