using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;

namespace Snowberry.DependencyInjection.Lookup;

/// <summary>
/// Default <see cref="IScope"/> implementation that resolves services through the owning
/// <see cref="ServiceContainer"/>, caches scoped instances for the lifetime of the scope, and disposes the
/// instances it owns when the scope is disposed.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultServiceScopeProvider"/> class.
    /// </summary>
    /// <param name="rootProvider">The container that resolves services for this scope.</param>
    /// <param name="isRootScope"><see langword="true"/> if this scope is the container's global scope; otherwise, <see langword="false"/>.</param>
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
    /// Attempts to retrieve a previously cached scoped instance for the specified <paramref name="serviceIdentifier"/>.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service to look up.</param>
    /// <param name="instance">When this method returns <see langword="true"/>, contains the cached instance; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a cached instance was found; otherwise, <see langword="false"/>.</returns>
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
    /// Caches <paramref name="instance"/> for the specified <paramref name="serviceIdentifier"/> in this scope.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service to cache.</param>
    /// <param name="instance">The instance to cache for the identifier.</param>
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
    /// thread already cached one.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service to cache.</param>
    /// <param name="instance">The instance to cache when no instance is already cached.</param>
    /// <param name="existing">When this method returns <see langword="false"/>, contains the instance that was already cached; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="instance"/> was stored; otherwise, <see langword="false"/>.</returns>
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
    /// Returns a new dictionary containing the entries of <paramref name="current"/> plus an entry mapping
    /// <paramref name="serviceIdentifier"/> to <paramref name="instance"/>.
    /// </summary>
    /// <param name="current">The existing entries to copy, or <see langword="null"/> to start empty.</param>
    /// <param name="serviceIdentifier">The identifier to add or overwrite.</param>
    /// <param name="instance">The instance to associate with the identifier.</param>
    /// <returns>A new dictionary with the combined entries.</returns>
    // Copy-on-write: the previous dictionary is left untouched so lock-free readers stay safe. Call under _lock.
    private static Dictionary<ServiceIdentifier, object> CloneWith(Dictionary<ServiceIdentifier, object>? current, in ServiceIdentifier serviceIdentifier, object instance)
    {
        var next = current == null
            ? new Dictionary<ServiceIdentifier, object>(1, ServiceIdentifierComparer.s_Instance)
            : new Dictionary<ServiceIdentifier, object>(current, ServiceIdentifierComparer.s_Instance);

        next[serviceIdentifier] = instance;
        return next;
    }

    /// <summary>
    /// Converts a public <see cref="IServiceIdentifier"/> into the concrete <see cref="ServiceIdentifier"/>
    /// value used as the scoped-cache key.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier to convert.</param>
    /// <returns>The equivalent <see cref="ServiceIdentifier"/>.</returns>
    private static ServiceIdentifier AsStruct(IServiceIdentifier serviceIdentifier)
    {
        return serviceIdentifier as ServiceIdentifier?
            ?? new ServiceIdentifier(serviceIdentifier.ServiceType, serviceIdentifier.ServiceKey);
    }

    /// <summary>
    /// Registers <paramref name="instance"/> so it is disposed together with this scope.
    /// </summary>
    /// <param name="instance">The disposable instance to track.</param>
    internal void TrackNewDisposable(object instance)
    {
        _disposableContainer.AddDisposableUnchecked(instance);
    }

    /// <summary>
    /// Marks the scope as disposed if it was not already, reporting whether disposal had already occurred.
    /// </summary>
    /// <returns><see langword="true"/> if the scope was already disposed before this call; otherwise, <see langword="false"/>.</returns>
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
