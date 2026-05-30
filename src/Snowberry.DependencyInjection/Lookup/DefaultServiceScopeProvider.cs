using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;

namespace Snowberry.DependencyInjection.Lookup;

/// <see cref="IServiceFactory"/>.
public class DefaultServiceScopeProvider : IScope, IServiceProvider, IKeyedServiceProvider
{
    /// <inheritdoc/>
    public event EventHandler? OnDispose;

    private readonly ServiceContainer _rootProvider;
    private readonly bool _isRootScope;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private bool _isDisposed;
    private DisposableContainer _disposableContainer = new();

    // Copy-on-write scoped-instance cache: the published dictionary is treated as IMMUTABLE — writers (under
    // _lock) publish a fresh clone, so warm reads need no lock. `volatile` gives readers the published snapshot
    // with correct memory ordering.
    private volatile Dictionary<ServiceIdentifier, object>? _scopedInstances;

    public DefaultServiceScopeProvider(ServiceContainer rootProvider, bool isRootScope)
    {
        _rootProvider = rootProvider;
        _isRootScope = isRootScope;
    }

    /// <inheritdoc/>
    public bool TryGetScopedInstance(IServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance)
    {
        return TryGetScopedInstance(AsStruct(serviceIdentifier), out instance);
    }

    /// <summary>
    /// Hot-path overload that takes the concrete <see cref="ServiceIdentifier"/> by reference so the scoped
    /// cache lookup does not box the struct key to <see cref="IServiceIdentifier"/>.
    /// </summary>
    internal bool TryGetScopedInstance(in ServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance)
    {
        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        // Lock-free read of the copy-on-write snapshot. The published dictionary is never mutated in place
        // (writers publish a fresh clone under _lock), so reading it concurrently with a writer is safe.
        var snapshot = _scopedInstances;
        if (snapshot != null && snapshot.TryGetValue(serviceIdentifier, out instance))
            return true;

        instance = null;
        return false;
    }

    /// <inheritdoc/>
    public void AddCached(IServiceIdentifier serviceIdentifier, object instance)
    {
        AddCached(AsStruct(serviceIdentifier), instance);
    }

    /// <summary>
    /// Hot-path overload that takes the concrete <see cref="ServiceIdentifier"/> by reference so caching does
    /// not box the struct key.
    /// </summary>
    internal void AddCached(in ServiceIdentifier serviceIdentifier, object instance)
    {
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            _scopedInstances = CloneWith(_scopedInstances, serviceIdentifier, instance);
        }
    }

    /// <summary>
    /// Atomically caches <paramref name="instance"/> for <paramref name="serviceIdentifier"/> unless another
    /// thread already cached one. Returns <c>true</c> when this instance won and was stored; returns
    /// <c>false</c> and the previously-cached <paramref name="existing"/> instance otherwise. Only the
    /// per-scope lock is taken — the caller constructs the instance *outside* any lock so a nested resolution
    /// that needs the container lock cannot invert lock order.
    /// </summary>
    internal bool TryAddScopedInstance(in ServiceIdentifier serviceIdentifier, object instance, out object? existing)
    {
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

            var current = _scopedInstances;
            if (current != null && current.TryGetValue(serviceIdentifier, out existing))
                return false;

            _scopedInstances = CloneWith(current, serviceIdentifier, instance);
            existing = null;
            return true;
        }
    }

    /// <summary>
    /// Copy-on-write helper: returns a new dictionary containing <paramref name="current"/>'s entries plus the
    /// added one. The previous dictionary is left untouched so concurrent lock-free readers stay safe. Called
    /// only under <see cref="_lock"/> (writers are serialized; the per-scope clone cost is paid on the cold path
    /// and is bounded by the number of distinct scoped services resolved in this scope).
    /// </summary>
    private static Dictionary<ServiceIdentifier, object> CloneWith(Dictionary<ServiceIdentifier, object>? current, in ServiceIdentifier serviceIdentifier, object instance)
    {
        var next = current == null
            ? new Dictionary<ServiceIdentifier, object>(1, ServiceIdentifierComparer.Instance)
            : new Dictionary<ServiceIdentifier, object>(current, ServiceIdentifierComparer.Instance);

        next[serviceIdentifier] = instance;
        return next;
    }

    /// <summary>
    /// Converts a public <see cref="IServiceIdentifier"/> to the concrete struct, unboxing when it already is
    /// one and reconstructing from its type/key otherwise (equality is by service type + key).
    /// </summary>
    private static ServiceIdentifier AsStruct(IServiceIdentifier serviceIdentifier)
    {
        return serviceIdentifier as ServiceIdentifier?
            ?? new ServiceIdentifier(serviceIdentifier.ServiceType, serviceIdentifier.ServiceKey);
    }

    /// <summary>
    /// Tracks a freshly-created disposable instance for this scope, bypassing the public dedupe scan
    /// (the instance is provably new). See <see cref="DisposableContainer.AddDisposableUnchecked"/>.
    /// </summary>
    internal void TrackNewDisposable(object instance)
    {
        _disposableContainer.AddDisposableUnchecked(instance);
    }

    /// <summary>
    /// Disposes the core.
    /// </summary>
    /// <returns>Whether the core is already disposed.</returns>
    private bool DisposeCore()
    {
        if (_isDisposed)
            return true;

        lock (_lock)
        {
            if (_isDisposed)
                return true;

            _isDisposed = true;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            _disposableContainer.Dispose();
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            await _disposableContainer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }
#endif

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        return GetKeyedService(serviceType, serviceKey: null);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return _rootProvider.GetKeyedService(serviceType, this, serviceKey);
    }

    /// <inheritdoc/>
    public bool IsDisposed
    {
        get
        {
            if (_isDisposed)
                return true;

            lock (_lock)
            {
                return _isDisposed;
            }
        }
    }

    /// <inheritdoc/>
    public IDisposableContainer DisposableContainer => _disposableContainer;

    /// <inheritdoc/>
    public IKeyedServiceProvider ServiceProvider => this;

    /// <inheritdoc/>
    public bool IsGlobalScope => _isRootScope;
}
