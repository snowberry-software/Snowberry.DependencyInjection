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
/// <summary>
/// Supplied by the container to the node compiler (Tier 2): returns the child resolver for a constructor
/// parameter / injectable property dependency, or <c>null</c> when the dependency is not registered (and is not
/// a built-in). The compiler bakes a throw (required) or the default (optional) for the <c>null</c> case.
/// A resolver is <c>Func&lt;DefaultServiceScopeProvider, object?&gt;</c> — fully public types, so the compiled
/// expression that invokes a captured child does not reference a non-public delegate.
/// </summary>
internal delegate Func<DefaultServiceScopeProvider, object?>? ChildResolverFactory(Type dependencyType, object? dependencyKey);

/// <inheritdoc cref="IServiceContainer"/>.
public partial class ServiceContainer : IServiceContainer
{
    private ConcurrentDictionary<ServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = new(ServiceIdentifierComparer.Instance);

    // Realized-resolver caches. The dominant null-key case is keyed by Type directly (skips ServiceIdentifier
    // construction + hash on the warm path); keyed services use the ServiceIdentifier cache. Invalidation swaps
    // both fields atomically (see InvalidateResolverCaches); reads capture the field once so a concurrent swap
    // is a benign "just-missed" window. `volatile` gives the swap acquire/release semantics.
    private volatile ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> _nullKeyResolvers = new();
    private volatile ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> _keyedResolvers = new(ServiceIdentifierComparer.Instance);

    // Tier 3 (opt-in): once frozen, registrations are locked and the compiled-resolver graph is permanent, which
    // lets the build inline pure-transient subtrees (no per-node delegate hop). Default is false (mutable).
    private volatile bool _frozen;

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
    /// Cold path for a null-key resolve: build (and publish) the resolver eagerly over its subtree, then invoke.
    /// Both cache generations are captured once so the whole subtree publishes consistently; a concurrent
    /// invalidation swap just makes this build land in the abandoned generation (harmless — the next resolve
    /// rebuilds). A duplicate build under contention is harmless (build is side-effect-free; last writer wins).
    /// </summary>
    private object? BuildAndCacheNullKey(Type serviceType, DefaultServiceScopeProvider scope, ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache)
    {
        var keyedCache = _keyedResolvers;
        var resolver = GetOrBuildResolver(serviceType, serviceKey: null, nullKeyCache, keyedCache, new List<ServiceIdentifier>());

        // Unregistered, non-built-in → GetService returns null (only GetRequiredService throws). Not cached, to
        // keep the cache bounded by registered/dependency identifiers (a later Register rebuilds on next resolve).
        return resolver is null ? null : resolver(scope);
    }

    /// <summary>
    /// Cold path for a keyed resolve. See <see cref="BuildAndCacheNullKey"/>.
    /// </summary>
    private object? BuildAndCacheKeyed(ServiceIdentifier serviceIdentifier, DefaultServiceScopeProvider scope, ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache)
    {
        var nullKeyCache = _nullKeyResolvers;
        var resolver = GetOrBuildResolver(serviceIdentifier.ServiceType, serviceIdentifier.ServiceKey, nullKeyCache, keyedCache, new List<ServiceIdentifier>());
        return resolver is null ? null : resolver(scope);
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
    /// Returns the resolver for a service from the captured cache generation, building (and publishing) it on a
    /// miss. Returns <c>null</c> only for an unregistered, non-built-in service — the node compiler then bakes a
    /// throw for a required dependency or the default for an optional one. Built recursively with eager subtree
    /// construction; <paramref name="buildPath"/> detects cycles (→ <see cref="CircularDependencyException"/>).
    /// Side-effect-free: it compiles/captures only and constructs no instance (singletons construct lazily on
    /// first invoke).
    /// </summary>
    private Func<DefaultServiceScopeProvider, object?>? GetOrBuildResolver(
        Type serviceType,
        object? serviceKey,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        List<ServiceIdentifier> buildPath)
    {
        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);

        // Cache check (the same generation captured at the root build), so shared subtrees build once.
        if (serviceKey is null)
        {
            if (nullKeyCache.TryGetValue(serviceType, out var cachedNull))
                return cachedNull;
        }
        else if (keyedCache.TryGetValue(serviceIdentifier, out var cachedKeyed))
        {
            return cachedKeyed;
        }

        Func<DefaultServiceScopeProvider, object?> resolver;

        // Built-in services resolve before user registrations, and only for a null key (matches legacy order).
        if (serviceKey is null && IsBuiltInService(serviceType))
        {
            resolver = scope => GetBuiltInService(serviceType, scope, null);
        }
        else
        {
            var serviceDescriptor = GetOptionalServiceDescriptor(serviceIdentifier);
            if (serviceDescriptor == null)
                return null; // unregistered, non-built-in

            // Cycle detection: re-entering an id already on the build path closes a cycle.
            if (buildPath.Contains(serviceIdentifier))
                throw new CircularDependencyException(serviceType, BuildCyclePath(buildPath, serviceType));

            buildPath.Add(serviceIdentifier);
            try
            {
                resolver = BuildLifetimeResolver(serviceIdentifier, serviceDescriptor, nullKeyCache, keyedCache, buildPath);
            }
            finally
            {
                buildPath.RemoveAt(buildPath.Count - 1);
            }
        }

        // Publish into the captured generation (built-in + registered). Idempotent under contention.
        if (serviceKey is null)
            nullKeyCache[serviceType] = resolver;
        else
            keyedCache[serviceIdentifier] = resolver;

        return resolver;
    }

    private static IReadOnlyList<Type> BuildCyclePath(List<ServiceIdentifier> buildPath, Type closing)
    {
        var path = new List<Type>(buildPath.Count + 1);
        for (int i = 0; i < buildPath.Count; i++)
            path.Add(buildPath[i].ServiceType);
        path.Add(closing);
        return path;
    }

    /// <summary>
    /// Wraps a node's pure construct delegate with the lifetime behavior (per-scope/singleton caching, locks,
    /// scope-validation, disposal tracking). The construct is the compiled graph node (Tier 2), a custom
    /// <c>InstanceFactory</c> call, or the by-type <see cref="IServiceFactory.CreateInstance"/> fallback.
    /// </summary>
    private Func<DefaultServiceScopeProvider, object?> BuildLifetimeResolver(
        ServiceIdentifier serviceIdentifier,
        IServiceDescriptor serviceDescriptor,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        List<ServiceIdentifier> buildPath)
    {
        // A pre-set singleton instance (e.g. RegisterSingleton<T>(instance)) needs NO construction — its
        // ImplementationType is typically the service interface, so building a construct would fail. Return the
        // constant directly; the instance is never constructed and never tracked for disposal.
        if (serviceDescriptor.Lifetime == ServiceLifetime.Singleton && serviceDescriptor.SingletonInstance != null)
        {
            object preset = serviceDescriptor.SingletonInstance;
            return _ => preset;
        }

        var construct = BuildConstruct(serviceIdentifier, serviceDescriptor, nullKeyCache, keyedCache, buildPath);

        switch (serviceDescriptor.Lifetime)
        {
            case ServiceLifetime.Transient:
                return BuildTransientResolver(construct);

            case ServiceLifetime.Scoped:
                return BuildScopedResolver(serviceIdentifier, construct);

            case ServiceLifetime.Singleton:
                return BuildSingletonResolver(serviceIdentifier, serviceDescriptor, construct, RebakeFor(serviceIdentifier, nullKeyCache, keyedCache));

            default:
                var lifetime = serviceDescriptor.Lifetime;
                return _ => ThrowHelper.ThrowServiceLifetimeNotImplemented(lifetime);
        }
    }

    /// <summary>
    /// Builds the pure construct delegate (returns a fresh, non-null instance): a custom <c>InstanceFactory</c>
    /// call, a compiled graph node that invokes captured child resolvers directly (default factory), or the
    /// by-type <see cref="IServiceFactory.CreateInstance"/> fallback (custom factory). The construct threads its
    /// scope argument to child resolvers; the lifetime wrapper chooses the scope (request vs. <c>RootScope</c>).
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private Func<DefaultServiceScopeProvider, object> BuildConstruct(
        ServiceIdentifier serviceIdentifier,
        IServiceDescriptor serviceDescriptor,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        List<ServiceIdentifier> buildPath)
    {
        var serviceKey = serviceIdentifier.ServiceKey;
        var closedGenericArguments = GetClosedGenericTypeArguments(serviceDescriptor);

        // Custom factory descriptor: call it live (not compiled), passing the threaded scope's provider so
        // scoped/keyed lookups inside the factory resolve in the correct scope. Preserve the legacy
        // `factory ?? CreateInstance` fall-through for a factory that returns null.
        var instanceFactory = serviceDescriptor.InstanceFactory;
        if (instanceFactory != null)
        {
            return scope => instanceFactory.Invoke(scope.ServiceProvider, serviceKey)
                ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, closedGenericArguments);
        }

        // Tier 2: compile a node that invokes the children's resolvers directly (no per-argument re-dispatch).
        if (ServiceFactory is DefaultServiceFactory defaultFactory)
        {
            // Tier 3 (frozen): additionally inline simple-transient children's construction (no per-node hop).
            Func<Type, object?, Type?>? shouldInline = _frozen
                ? (dependencyType, dependencyKey) => FrozenInlineType(dependencyType, dependencyKey, defaultFactory)
                : null;

            return defaultFactory.CompileNode(
                serviceDescriptor.ImplementationType,
                closedGenericArguments,
                (dependencyType, dependencyKey) => GetOrBuildResolver(dependencyType, dependencyKey, nullKeyCache, keyedCache, buildPath),
                shouldInline);
        }

        // Custom IServiceFactory: fall back to by-type construction (children re-dispatch through GetKeyedService).
        return scope => ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, closedGenericArguments);
    }

    /// <summary>
    /// Frozen-mode inline selector: returns the closed implementation type to inline when the dependency is a
    /// simple transient (constructor-backed, non-disposable, no <c>[Inject]</c> properties), else <c>null</c>
    /// (the dependency goes through its captured resolver — preserving scoped/singleton caching, factories,
    /// disposal tracking, built-ins, and unregistered throw/default behavior).
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The closed implementation type carries the same DynamicallyAccessedMembers requirements as the descriptor's annotated ImplementationType.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private Type? FrozenInlineType(Type dependencyType, object? dependencyKey, DefaultServiceFactory defaultFactory)
    {
        if (dependencyKey is null && IsBuiltInService(dependencyType))
            return null;

        var dependencyIdentifier = new ServiceIdentifier(dependencyType, dependencyKey);
        var dependencyDescriptor = GetOptionalServiceDescriptor(dependencyIdentifier);
        if (dependencyDescriptor == null)
            return null;

        if (dependencyDescriptor.Lifetime != ServiceLifetime.Transient || dependencyDescriptor.InstanceFactory != null)
            return null;

        var implementationType = dependencyDescriptor.ImplementationType;
        var closedImplementationType = implementationType.IsGenericTypeDefinition
            ? implementationType.MakeGenericType(dependencyIdentifier.ServiceType.GenericTypeArguments)
            : implementationType;

        return defaultFactory.IsInlinableTransientImplementation(closedImplementationType) ? closedImplementationType : null;
    }

    private Func<DefaultServiceScopeProvider, object?> BuildTransientResolver(Func<DefaultServiceScopeProvider, object> construct)
    {
        return scope =>
        {
            object instance = construct(scope);

            if (instance.IsDisposable())
                scope.TrackNewDisposable(instance);

            return instance;
        };
    }

    private Func<DefaultServiceScopeProvider, object?> BuildScopedResolver(ServiceIdentifier serviceIdentifier, Func<DefaultServiceScopeProvider, object> construct)
    {
        var serviceType = serviceIdentifier.ServiceType;

        // ValidateScopes is fixed for the container's life (Options is get-only), so capture it once.
        bool validateScope = ValidateScopes;

        return scope =>
        {
            // The validation lives in the wrapper so it fires on the warm path AND for children reached via a
            // captured resolver (which never re-enter the GetKeyedService entry check).
            if (validateScope && scope.IsGlobalScope)
                throw new ServiceScopeRequiredException(serviceType);

            if (scope.TryGetScopedInstance(serviceIdentifier, out object? existing))
                return existing!;

            // Construct OUTSIDE any lock (deadlock-free; see TryAddScopedInstance).
            object created = construct(scope);

            bool won = scope.TryAddScopedInstance(serviceIdentifier, created, out object? raced);

            // Track whether we won or lost so a disposable loser is disposed at scope teardown, not leaked.
            if (created.IsDisposable())
                scope.TrackNewDisposable(created);

            return won ? created : raced!;
        };
    }

    private Func<DefaultServiceScopeProvider, object?> BuildSingletonResolver(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, Func<DefaultServiceScopeProvider, object> construct, Action<Func<DefaultServiceScopeProvider, object?>> rebake)
    {
        // A pre-set instance (e.g. RegisterSingleton<T>(instance)) is never constructed and never tracked.
        if (serviceDescriptor.SingletonInstance != null)
        {
            object preset = serviceDescriptor.SingletonInstance;
            return _ => preset;
        }

        return scope =>
        {
            // The singleton subtree is rooted at RootScope: construct/track use RootScope, never the caller's
            // scope (the node threads RootScope to its children), reproducing the legacy construction.
            if (serviceDescriptor.SingletonInstance == null)
            {
                lock (_lock)
                {
                    if (serviceDescriptor.SingletonInstance == null)
                    {
                        serviceDescriptor.SingletonInstance = construct(RootScope);

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
    /// Produces the singleton constant-bake action that publishes the resolved instance into the captured cache
    /// generation for <paramref name="serviceIdentifier"/>.
    /// </summary>
    private static Action<Func<DefaultServiceScopeProvider, object?>> RebakeFor(
        ServiceIdentifier serviceIdentifier,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache)
    {
        if (serviceIdentifier.ServiceKey is null)
        {
            var serviceType = serviceIdentifier.ServiceType;
            return constant => nullKeyCache[serviceType] = constant;
        }

        return constant => keyedCache[serviceIdentifier] = constant;
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

    /// <summary>
    /// Gets whether the container has been frozen via <see cref="Freeze"/>. A frozen container rejects further
    /// registration changes and uses the immutable, fully-inlined frozen resolver pipeline.
    /// </summary>
    public bool IsFrozen => _frozen;

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