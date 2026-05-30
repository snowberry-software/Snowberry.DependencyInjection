using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Helper;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Lookup;

namespace Snowberry.DependencyInjection;

/// <summary>
/// A realized resolver for a single service: given the current resolution scope it returns the instance
/// (or <c>null</c> for an unregistered service). Built on the first (cold) resolve and cached; the warm path
/// is a single dictionary lookup + this invoke. Must be side-effect-free to build — it may construct
/// instances only when invoked, never at build time.
/// </summary>
internal delegate object? ServiceResolver(DefaultServiceScopeProvider scope);

/// <inheritdoc cref="IServiceContainer"/>.
public partial class ServiceContainer : IServiceContainer
{
    private ConcurrentDictionary<ServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = new(ServiceIdentifierComparer.Instance);

    // Realized-resolver caches. The dominant null-key case is keyed by Type directly (skips ServiceIdentifier
    // construction + hash on the warm path); keyed services use the ServiceIdentifier cache. Invalidation swaps
    // both fields atomically (see InvalidateResolverCaches); reads capture the field once so a concurrent swap
    // is a benign "just-missed" window. `volatile` gives the swap acquire/release semantics.
    private volatile ConcurrentDictionary<Type, ServiceResolver> _nullKeyResolvers = new();
    private volatile ConcurrentDictionary<ServiceIdentifier, ServiceResolver> _keyedResolvers = new(ServiceIdentifierComparer.Instance);

    private bool _isDisposed;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private DefaultServiceScopeProvider _rootScope;
    private IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Creates a container with the default options.
    /// </summary>
    public ServiceContainer() : this(null!, ServiceContainerOptions.Default)
    {
    }

    /// <summary>
    /// Creates a new container using the given <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options for the container.</param>
    public ServiceContainer(ServiceContainerOptions options) : this(null!, options)
    {
    }

    /// <summary>
    /// Creates a new container using the given <paramref name="serviceFactory"/> and <paramref name="options"/>.
    /// </summary>
    /// <param name="serviceFactory">The service factory that will be used.</param>
    /// <param name="options">The options for the container.</param>
    public ServiceContainer(IServiceFactory serviceFactory, ServiceContainerOptions options)
    {
        ServiceFactory = serviceFactory ?? new DefaultServiceFactory(this);
        Options = options;

        _serviceScopeFactory = new DefaultServiceScopeFactory(this);
        _rootScope = new(this, isRootScope: true);
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

        lock (_lock)
        {
            _rootScope.Dispose();
        }
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        ValueTask disposeTask;
        lock (_lock)
        {
            disposeTask = _rootScope.DisposeAsync();
        }

        await disposeTask.ConfigureAwait(false);
    }
#endif

    /// <inheritdoc/>
    public IServiceDescriptor? GetOptionalServiceDescriptor(Type serviceType, object? serviceKey)
    {
        return GetOptionalServiceDescriptor(new ServiceIdentifier(serviceType, serviceKey));
    }

    /// <summary>
    /// Looks up the descriptor for an already-built <see cref="ServiceIdentifier"/>, avoiding a second
    /// identifier construction/hash on the resolve hot path.
    /// </summary>
    private IServiceDescriptor? GetOptionalServiceDescriptor(in ServiceIdentifier serviceIdentifier)
    {
        // Fast path: try to read without any locking first (using ConcurrentDictionary's thread safety)
        if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out var serviceDescriptor))
        {
            DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
            return serviceDescriptor;
        }

        var serviceType = serviceIdentifier.ServiceType;

        // If not found and it's not a constructed generic, return null without locking.
        // IsConstructedGenericType avoids the Type[] copy that GenericTypeArguments allocates for closed generics.
        if (!serviceType.IsConstructedGenericType)
        {
            DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
            return null;
        }

        // For generic types, we need to check for open generic registrations and potentially create new descriptors
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);

            // Double-check if the service was registered while we were waiting for the lock
            if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out serviceDescriptor))
                return serviceDescriptor;

            var genericType = serviceType.GetGenericTypeDefinition();
            var genericServiceIdentifier = new ServiceIdentifier(genericType, serviceIdentifier.ServiceKey);

            // Check if we have an open generic registration
            if (_serviceDescriptorMapping.TryGetValue(genericServiceIdentifier, out serviceDescriptor))
            {
                var newServiceDescriptor = serviceDescriptor.CloneFor(serviceType);

                // CRITICAL: this is an additive cache of a closed descriptor cloned from an existing open-generic
                // registration (reached only after a confirmed miss). It never changes/removes a binding and runs
                // mid-resolve, so it must NOT invalidate the resolver caches (no InvalidateResolverCaches here).
                _serviceDescriptorMapping.AddOrUpdate(serviceIdentifier, newServiceDescriptor, (_, _) => newServiceDescriptor);
                return newServiceDescriptor;
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public IServiceDescriptor GetServiceDescriptor(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
        var descriptor = GetOptionalServiceDescriptor(serviceType, serviceKey);
        return descriptor ?? throw new ServiceTypeNotRegistered(serviceType);
    }

    /// <inheritdoc/>
    public IServiceDescriptor GetServiceDescriptor<T>(object? serviceKey)
    {
        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
        return GetServiceDescriptor(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public IServiceDescriptor? GetOptionalServiceDescriptor<T>(object? serviceKey)
    {
        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
        return GetOptionalServiceDescriptor(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return GetKeyedService(serviceType, RootScope, serviceKey: null);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return GetKeyedService(serviceType, RootScope, serviceKey);
    }

    internal object? GetKeyedService(Type serviceType, DefaultServiceScopeProvider scope, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = scope ?? throw new ArgumentNullException(nameof(scope));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        if (serviceKey is null)
        {
            // Dominant case: key the cache by Type directly, avoiding a ServiceIdentifier construction + hash.
            var cache = _nullKeyResolvers;
            if (cache.TryGetValue(serviceType, out var resolver))
                return resolver(scope);

            return BuildAndCacheNullKey(serviceType, scope, cache);
        }
        else
        {
            var cache = _keyedResolvers;
            var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
            if (cache.TryGetValue(serviceIdentifier, out var resolver))
                return resolver(scope);

            return BuildAndCacheKeyed(serviceIdentifier, scope, cache);
        }
    }

    /// <summary>
    /// Cold path for a null-key resolve: build the resolver, publish it into the captured generation, invoke.
    /// A duplicate build under contention is harmless (build is side-effect-free; last writer wins).
    /// </summary>
    private object? BuildAndCacheNullKey(Type serviceType, DefaultServiceScopeProvider scope, ConcurrentDictionary<Type, ServiceResolver> cache)
    {
        var resolver = BuildResolver(serviceType, serviceKey: null, rebake: constant => cache[serviceType] = constant);
        cache[serviceType] = resolver;
        return resolver(scope);
    }

    /// <summary>
    /// Cold path for a keyed resolve. See <see cref="BuildAndCacheNullKey"/>.
    /// </summary>
    private object? BuildAndCacheKeyed(ServiceIdentifier serviceIdentifier, DefaultServiceScopeProvider scope, ConcurrentDictionary<ServiceIdentifier, ServiceResolver> cache)
    {
        var resolver = BuildResolver(serviceIdentifier.ServiceType, serviceIdentifier.ServiceKey, rebake: constant => cache[serviceIdentifier] = constant);
        cache[serviceIdentifier] = resolver;
        return resolver(scope);
    }

    private object? GetBuiltInService(Type serviceType, DefaultServiceScopeProvider scope, object? serviceKey)
    {
        if (typeof(IServiceContainer).IsAssignableFrom(serviceType))
            return this;

        if (typeof(IServiceRegistry).IsAssignableFrom(serviceType))
            return this;

        if (typeof(IServiceDescriptorReceiver).IsAssignableFrom(serviceType))
            return this;

        if (typeof(IServiceProvider).IsAssignableFrom(serviceType))
            return scope;

        if (typeof(IScope).IsAssignableFrom(serviceType))
            return scope;

        if (typeof(IServiceScopeFactory).IsAssignableFrom(serviceType))
            return _serviceScopeFactory;

        if (typeof(IServiceFactory).IsAssignableFrom(serviceType))
            return ServiceFactory;

        throw new NotImplementedException($"Built-in service of type '{serviceType.FullName}' is not implemented.");
    }

    /// <summary>
    /// Memoizes the built-in-service classification per <see cref="Type"/>. The result is fixed for a given
    /// type, so the seven <see cref="Type.IsAssignableFrom(Type)"/> probes only run on the first resolve of
    /// each type; every later null-key resolve is a single cache hit. Behavior is identical to the ladder
    /// (including the derived-interface case) — only the cost is amortized.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> _builtInServiceCache = new();

    private static bool IsBuiltInService(Type serviceType)
    {
        return _builtInServiceCache.GetOrAdd(serviceType, static type =>
            typeof(IServiceContainer).IsAssignableFrom(type)
            || typeof(IServiceRegistry).IsAssignableFrom(type)
            || typeof(IServiceDescriptorReceiver).IsAssignableFrom(type)
            || typeof(IServiceProvider).IsAssignableFrom(type)
            || typeof(IScope).IsAssignableFrom(type)
            || typeof(IServiceScopeFactory).IsAssignableFrom(type)
            || typeof(IServiceFactory).IsAssignableFrom(type));
    }

    /// <summary>
    /// Cold path: classifies a service (built-in / registered / unregistered) and emits a lifetime-specialized
    /// <see cref="ServiceResolver"/>. Side-effect-free — it compiles closures only; no instance is constructed
    /// here (singletons construct lazily on first invoke). <paramref name="rebake"/> lets the singleton resolver
    /// publish a constant resolver into its cache generation after first construction.
    /// </summary>
    private ServiceResolver BuildResolver(Type serviceType, object? serviceKey, Action<ServiceResolver> rebake)
    {
        // Built-in services resolve before user registrations, and only for a null key (matches legacy order).
        if (serviceKey is null && IsBuiltInService(serviceType))
            return scope => GetBuiltInService(serviceType, scope, null);

        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
        var serviceDescriptor = GetOptionalServiceDescriptor(serviceIdentifier);

        // Unregistered → GetService returns null (only the GetRequiredService extension throws). Safe to cache:
        // any Register/Unregister swaps the whole resolver cache (see InvalidateResolverCaches).
        if (serviceDescriptor == null)
            return static _ => null;

        // Hoist the closed-generic argument computation out of the per-resolve path (immutable for a descriptor).
        var closedGenericArguments = GetClosedGenericTypeArguments(serviceDescriptor);

        // Pass the annotated ImplementationType property directly so its DynamicallyAccessedMembers flow to the
        // factory call without an intermediate (unannotated) local.
        switch (serviceDescriptor.Lifetime)
        {
            case ServiceLifetime.Singleton:
                return BuildSingletonResolver(serviceIdentifier, serviceDescriptor, serviceDescriptor.ImplementationType, closedGenericArguments, rebake);

            case ServiceLifetime.Transient:
                return BuildTransientResolver(serviceIdentifier, serviceDescriptor, serviceDescriptor.ImplementationType, closedGenericArguments);

            case ServiceLifetime.Scoped:
                return BuildScopedResolver(serviceIdentifier, serviceDescriptor, serviceDescriptor.ImplementationType, closedGenericArguments);

            default:
                var lifetime = serviceDescriptor.Lifetime;
                return _ => ThrowHelper.ThrowServiceLifetimeNotImplemented(lifetime);
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private ServiceResolver BuildTransientResolver(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType, Type[]? closedGenericArguments)
    {
        var serviceKey = serviceIdentifier.ServiceKey;

        return scope =>
        {
            object instance = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceKey)
                ?? ServiceFactory.CreateInstance(implementationType, scope.ServiceProvider, closedGenericArguments);

            if (instance.IsDisposable())
                scope.TrackNewDisposable(instance);

            return instance;
        };
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private ServiceResolver BuildScopedResolver(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType, Type[]? closedGenericArguments)
    {
        var serviceKey = serviceIdentifier.ServiceKey;
        var serviceType = serviceIdentifier.ServiceType;

        // ValidateScopes is fixed for the container's life (Options is get-only), so capture it once.
        bool validateScope = ValidateScopes;

        return scope =>
        {
            // The validation lives in the wrapper so it fires on the warm path AND for children reached via a
            // captured resolver (which never re-enter the GetKeyedService entry check).
            if (validateScope && scope.IsGlobalScope)
                throw new ServiceScopeRequiredException(serviceType);

            if (scope.TryGetScopedInstance(serviceIdentifier, out object? instance))
                return instance!;

            // Construct OUTSIDE any lock (deadlock-free; see TryAddScopedInstance).
            object created = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceKey)
                ?? ServiceFactory.CreateInstance(implementationType, scope.ServiceProvider, closedGenericArguments);

            bool won = scope.TryAddScopedInstance(serviceIdentifier, created, out object? existing);

            // Track whether we won or lost so a disposable loser is disposed at scope teardown, not leaked.
            if (created.IsDisposable())
                scope.TrackNewDisposable(created);

            return won ? created : existing!;
        };
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private ServiceResolver BuildSingletonResolver(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType, Type[]? closedGenericArguments, Action<ServiceResolver> rebake)
    {
        var serviceKey = serviceIdentifier.ServiceKey;

        // A pre-set instance (e.g. RegisterSingleton<T>(instance)) is never constructed and never tracked.
        if (serviceDescriptor.SingletonInstance != null)
        {
            object preset = serviceDescriptor.SingletonInstance;
            return _ => preset;
        }

        return scope =>
        {
            // The singleton subtree is rooted at RootScope: construct/track use RootScope, never the caller's
            // scope, reproducing the legacy RootScope.ServiceProvider construction.
            if (serviceDescriptor.SingletonInstance == null)
            {
                lock (_lock)
                {
                    if (serviceDescriptor.SingletonInstance == null)
                    {
                        serviceDescriptor.SingletonInstance = serviceDescriptor.InstanceFactory?.Invoke(RootScope.ServiceProvider, serviceKey)
                            ?? ServiceFactory.CreateInstance(implementationType, RootScope.ServiceProvider, closedGenericArguments);

                        if (serviceDescriptor.SingletonInstance.IsDisposable())
                            RootScope.TrackNewDisposable(serviceDescriptor.SingletonInstance);
                    }
                }
            }

            object instance = serviceDescriptor.SingletonInstance!;

            // Constant-bake: warm resolves now return the instance directly (no descriptor virtual read). Written
            // into the captured generation; if a mutation already swapped the cache this lands in the abandoned
            // generation and is harmless.
            rebake(_ => instance);
            return instance;
        };
    }

    /// <summary>
    /// Returns the closed generic type arguments only when the implementation is an open generic definition
    /// that <see cref="IServiceFactory.CreateInstance"/> must close via <c>MakeGenericType</c>. For every other
    /// (non-generic) implementation the factory ignores the argument, so we avoid the <c>Type[]</c> the
    /// <see cref="Type.GenericTypeArguments"/> property would otherwise copy on each resolve.
    /// </summary>
    private static Type[]? GetClosedGenericTypeArguments(IServiceDescriptor serviceDescriptor)
    {
        return serviceDescriptor.ImplementationType.IsGenericTypeDefinition
            ? serviceDescriptor.ServiceType.GenericTypeArguments
            : null;
    }

    /// <summary>
    /// Drops every compiled resolver by swapping in fresh, empty caches. Called after a registration mutation
    /// (<see cref="Register"/> / unregister) under <see cref="_lock"/>. In-flight resolves finish against the
    /// generation they captured; new resolves rebuild lazily against the empty caches. NOTE: this is the only
    /// place the resolver caches are invalidated — the closed-generic auto-cache in
    /// <see cref="GetOptionalServiceDescriptor(in ServiceIdentifier)"/> must NOT call it.
    /// </summary>
    private void InvalidateResolverCaches()
    {
        _nullKeyResolvers = new();
        _keyedResolvers = new(ServiceIdentifierComparer.Instance);
    }

    /// <summary>
    /// The service factory that will be used.
    /// </summary>
    public IServiceFactory ServiceFactory { get; }

    /// <inheritdoc/>
    public IServiceDescriptor[] GetServiceDescriptors()
    {
        lock (_lock)
        {
            return [.. _serviceDescriptorMapping.Values];
        }
    }

    /// <inheritdoc/>
    public IEnumerator<IServiceDescriptor> GetEnumerator()
    {
        lock (_lock)
        {
            return _serviceDescriptorMapping.Values.GetEnumerator();
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _serviceDescriptorMapping.Count;
            }
        }
    }

    /// <summary>
    /// Gets the options that were used for the container.
    /// </summary>
    public ServiceContainerOptions Options { get; }

    /// <summary>
    /// Gets whether the registered services are read-only and can't be overwritten.
    /// </summary>
    public bool AreRegisteredServicesReadOnly => (Options & ServiceContainerOptions.ReadOnly) == ServiceContainerOptions.ReadOnly;

    /// <summary>
    /// Gets whether scope validation is enabled.
    /// </summary>
    public bool ValidateScopes => (Options & ServiceContainerOptions.ValidateScopes) == ServiceContainerOptions.ValidateScopes;

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
    public IDisposableContainer DisposableContainer => RootScope.DisposableContainer;

    /// <summary>
    /// Gets the root scope.
    /// </summary>
    internal DefaultServiceScopeProvider RootScope => _rootScope;
}