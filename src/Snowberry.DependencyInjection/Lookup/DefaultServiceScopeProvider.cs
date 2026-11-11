using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
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

    // Scoped instances live in a small copy-on-write array (linear scan) until there are this many, then move to a
    // ConcurrentDictionary. Most scopes hold a handful of scoped services; the array keeps them cheap to create
    // and read, while the dictionary keeps large scopes from paying a full copy on every new instance.
    private const int MaxScopedEntries = 8;

    // Shared stand-in installed by Dispose when the scope never tracked a disposable, so disposal and late
    // tracking see a disposed container without allocating one per scope.
    private static readonly DisposableContainer s_DisposedContainer = Snowberry.DependencyInjection.DisposableContainer.CreateDisposed();

    private readonly ServiceContainer _rootProvider;
    private readonly bool _isRootScope;

    // Created on first write; scopes that only read cached instances or resolve transients never allocate it.
#if NET9_0_OR_GREATER
    private Lock? _lock;
#else
    private object? _lock;
#endif

    private volatile bool _isDisposed;
    private int _disposeStarted;

    // Created on first tracked disposable (or first DisposableContainer access); see s_DisposedContainer.
    private DisposableContainer? _disposableContainer;

    // Scoped-instance store. Exactly one of the two is in use: _scopedEntries (published arrays are IMMUTABLE;
    // writers publish a fresh copy under the lock) until it would exceed MaxScopedEntries, then _scopedMap.
    // Reads are lock-free; `volatile` gives readers the published snapshot with correct memory ordering.
    private volatile ScopedEntry[]? _scopedEntries;
    private volatile ConcurrentDictionary<ServiceIdentifier, object>? _scopedMap;

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
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    internal bool TryGetScopedInstance(in ServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance)
    {
        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        // Lock-free read of the published snapshot. A reader racing the move to the dictionary may still see the
        // old array and miss; the caller then takes the locked TryAddScopedInstance path, which is authoritative.
        var entries = _scopedEntries;
        if (entries != null)
            return TryFind(entries, serviceIdentifier, out instance);

        var map = _scopedMap;
        if (map != null)
            return map.TryGetValue(serviceIdentifier, out instance);

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
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    internal void AddCached(in ServiceIdentifier serviceIdentifier, object instance)
    {
        lock (SyncRoot)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            SetScopedInstanceLocked(serviceIdentifier, instance);
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
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    internal bool TryAddScopedInstance(in ServiceIdentifier serviceIdentifier, object instance, out object? existing)
    {
        lock (SyncRoot)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

            var map = _scopedMap;
            if (map != null ? map.TryGetValue(serviceIdentifier, out existing) : TryFind(_scopedEntries, serviceIdentifier, out existing))
                return false;

            SetScopedInstanceLocked(serviceIdentifier, instance);
            existing = null;
            return true;
        }
    }

    /// <summary>
    /// Stores <paramref name="instance"/> for <paramref name="serviceIdentifier"/>, replacing an existing entry.
    /// Must be called while holding <see cref="SyncRoot"/>.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier to add or overwrite.</param>
    /// <param name="instance">The instance to associate with the identifier.</param>
    private void SetScopedInstanceLocked(in ServiceIdentifier serviceIdentifier, object instance)
    {
        var map = _scopedMap;
        if (map != null)
        {
            map[serviceIdentifier] = instance;
            return;
        }

        // Copy-on-write: published arrays are never mutated, so lock-free readers stay safe.
        var entries = _scopedEntries;
        if (entries == null)
        {
            _scopedEntries = [new ScopedEntry(serviceIdentifier, instance)];
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Identifier.Equals(serviceIdentifier))
            {
                var replaced = (ScopedEntry[])entries.Clone();
                replaced[i] = new ScopedEntry(serviceIdentifier, instance);
                _scopedEntries = replaced;
                return;
            }
        }

        if (entries.Length < MaxScopedEntries)
        {
            var grown = new ScopedEntry[entries.Length + 1];
            Array.Copy(entries, grown, entries.Length);
            grown[entries.Length] = new ScopedEntry(serviceIdentifier, instance);
            _scopedEntries = grown;
            return;
        }

        // Too many for a linear scan: move to a dictionary. Publish it before retiring the array so a reader that
        // sees the array gone always finds the dictionary.
        var created = new ConcurrentDictionary<ServiceIdentifier, object>(concurrencyLevel: 1, capacity: MaxScopedEntries * 4);
        for (int i = 0; i < entries.Length; i++)
            created[entries[i].Identifier] = entries[i].Instance;

        created[serviceIdentifier] = instance;
        _scopedMap = created;
        _scopedEntries = null;
    }

    /// <summary>
    /// Finds the instance cached for <paramref name="serviceIdentifier"/> in <paramref name="entries"/>.
    /// </summary>
    /// <param name="entries">The published entries, or <see langword="null"/> when none are cached.</param>
    /// <param name="serviceIdentifier">The identifier to look up.</param>
    /// <param name="instance">When this method returns <see langword="true"/>, contains the cached instance; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an entry was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryFind(ScopedEntry[]? entries, in ServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Identifier.Equals(serviceIdentifier))
                {
                    instance = entries[i].Instance;
                    return true;
                }
            }
        }

        instance = null;
        return false;
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
    /// <exception cref="ObjectDisposedException">The scope has been disposed; <paramref name="instance"/> is disposed before the exception is thrown.</exception>
    internal void TrackNewDisposable(object instance)
    {
        GetOrCreateDisposableContainer().AddDisposableUnchecked(instance);
    }

    /// <summary>
    /// Gets the scope's disposable container, creating it on first use.
    /// </summary>
    /// <returns>The container, or the shared disposed container when the scope was disposed before any was created.</returns>
    private DisposableContainer GetOrCreateDisposableContainer()
    {
        var container = _disposableContainer;
        if (container != null)
            return container;

        var created = new DisposableContainer();
        return Interlocked.CompareExchange(ref _disposableContainer, created, null) ?? created;
    }

    /// <summary>
    /// Gets the lock guarding scoped-instance writes, creating it on first use.
    /// </summary>
#if NET9_0_OR_GREATER
    private Lock SyncRoot => _lock ?? CreateLock();
#else
    private object SyncRoot => _lock ?? CreateLock();
#endif

    /// <summary>
    /// Creates and publishes the scope's lock.
    /// </summary>
    /// <returns>The published lock.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
#if NET9_0_OR_GREATER
    private Lock CreateLock()
    {
        Interlocked.CompareExchange(ref _lock, new Lock(), null);
#else
    private object CreateLock()
    {
        Interlocked.CompareExchange(ref _lock, new object(), null);
#endif
        return _lock!;
    }

    /// <summary>
    /// Marks the scope as disposed if it was not already, reporting whether disposal had already occurred.
    /// </summary>
    /// <returns><see langword="true"/> if the scope was already disposed before this call; otherwise, <see langword="false"/>.</returns>
    private bool DisposeCore()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return true;

        _isDisposed = true;
        return false;
    }

    /// <summary>
    /// Detaches the disposable container for disposal, installing the shared disposed container when none was
    /// created so later tracking is rejected.
    /// </summary>
    /// <returns>The container to dispose, or <see langword="null"/> when the scope never tracked a disposable.</returns>
    private DisposableContainer? TakeDisposableContainer()
    {
        var container = Interlocked.CompareExchange(ref _disposableContainer, s_DisposedContainer, null);
        return container;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Disposes the instances the scope created, in reverse creation order, then raises <see cref="OnDispose"/>.
    /// Every instance is disposed even when some of them throw.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A tracked instance only implements <c>IAsyncDisposable</c>; use <c>DisposeAsync</c> instead. The other instances are still disposed.</exception>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose; the inner exceptions are in disposal order. A single failure is rethrown unchanged.</exception>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            TakeDisposableContainer()?.Dispose();
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }

#if NETCOREAPP
    /// <inheritdoc/>
    /// <remarks>
    /// Disposes the instances the scope created, in reverse creation order, then raises <see cref="OnDispose"/>.
    /// Every instance is disposed even when some of them throw.
    /// </remarks>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose; the inner exceptions are in disposal order. A single failure is rethrown unchanged.</exception>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            var container = TakeDisposableContainer();
            if (container != null)
                await container.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }
#endif

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The scope or its container has been disposed.</exception>
    /// <exception cref="ServiceTypeNotRegistered">A required dependency of the service is not registered.</exception>
    /// <exception cref="CircularDependencyException">The service depends on itself through its dependencies.</exception>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return _rootProvider.GetKeyedService(serviceType, this, serviceKey: null);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The scope or its container has been disposed.</exception>
    /// <exception cref="ServiceTypeNotRegistered">A required dependency of the service is not registered.</exception>
    /// <exception cref="CircularDependencyException">The service depends on itself through its dependencies.</exception>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return _rootProvider.GetKeyedService(serviceType, this, serviceKey);
    }

    /// <inheritdoc/>
    public bool IsDisposed => _isDisposed;

    /// <inheritdoc/>
    public IDisposableContainer DisposableContainer => GetOrCreateDisposableContainer();

    /// <inheritdoc/>
    public IKeyedServiceProvider ServiceProvider => this;

    /// <inheritdoc/>
    public bool IsGlobalScope => _isRootScope;

    /// <summary>
    /// A cached scoped instance and the identifier it was resolved for.
    /// </summary>
    private readonly struct ScopedEntry(ServiceIdentifier identifier, object instance)
    {
        /// <summary>The identifier the instance was resolved for.</summary>
        public readonly ServiceIdentifier Identifier = identifier;

        /// <summary>The cached instance.</summary>
        public readonly object Instance = instance;
    }
}
