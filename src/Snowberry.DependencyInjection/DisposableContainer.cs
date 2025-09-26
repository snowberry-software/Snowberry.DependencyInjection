using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection;

/// <summary>
/// Container for storing disposables.
/// </summary>
internal class DisposableContainer : IDisposableContainer
{
    /// <summary>
    /// Objects either implementing <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.
    /// </summary>
    private List<object>? _disposables;
    private bool _isDisposed;
    private object _locker = new();

    /// <inheritdoc/>
    public void RegisterDisposable(IDisposable disposable)
    {
        RegisterDisposable((object)disposable);
    }

    /// <summary>
    /// Removes the specified instance from the collection.
    /// </summary>
    /// <param name="instance">The instance.</param>
    public void Remove(object? instance)
    {
        if (instance == null)
            return;

        _disposables?.Remove(instance);
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public void RegisterDisposable(IAsyncDisposable disposable)
    {
        RegisterDisposable((object)disposable);
    }
#endif

    /// <inheritdoc/>
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

        lock (_locker)
        {
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

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_locker)
        {
            if (IsDisposed)
                return;

            _isDisposed = true;

            if (_disposables == null || _disposables.Count == 0)
                return;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                object disposableObj = _disposables[i];

                if (disposableObj is IDisposable disposable)
                {
                    disposable.Dispose();
                    continue;
                }

                throw new InvalidOperationException($"`{disposableObj.GetType().FullName}` type only implements IAsyncDisposable. Use `DisposeAsync` to dispose the container.");
            }
        }
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_locker)
        {
            if (IsDisposed)
                return default;

            _isDisposed = true;

            if (_disposables == null || _disposables.Count == 0)
                return default;

            try
            {
                for (int i = _disposables.Count - 1; i >= 0; i--)
                {
                    object disposable = _disposables[i];

                    if (disposable is IAsyncDisposable asyncDisposable)
                    {
                        var vt = asyncDisposable.DisposeAsync();

                        if (!vt.IsCompletedSuccessfully)
                            return Await(i, vt, _disposables);

                        vt.GetAwaiter().GetResult();

                        continue;
                    }

                    ((IDisposable)disposable).Dispose();
                }
            }
            catch (Exception e)
            {
                return new ValueTask(Task.FromException(e));
            }

            return default;
        }

        static async ValueTask Await(int i, ValueTask vt, List<object> toDispose)
        {
            await vt.ConfigureAwait(false);

            // NOTE(VNC): Go to next element.
            i--;

            for (; i >= 0; i--)
            {
                object disposable = toDispose[i];

#if NETCOREAPP
                if (disposable is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    continue;
                }
#endif

                ((IDisposable)disposable).Dispose();
            }
        }
    }
#endif

    /// <summary>
    /// The amount of registered disposables.
    /// </summary>
    public int DisposableCount
    {
        get
        {
            lock (_locker)
            {
                return _disposables?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// Determines whether the container is disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_locker)
                return _isDisposed;
        }
    }
}