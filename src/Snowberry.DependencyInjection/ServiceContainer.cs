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
/// Resolves the child resolver for a single dependency given its type and optional key, or returns
/// <see langword="null"/> when the dependency is not registered and is not a built-in service.
/// </summary>
/// <param name="dependencyType">The dependency service type to resolve a resolver for.</param>
/// <param name="dependencyKey">The optional service key of the dependency, or <see langword="null"/> for an unkeyed dependency.</param>
/// <returns>
/// A resolver that produces the dependency instance for a given <see cref="DefaultServiceScopeProvider"/>,
/// or <see langword="null"/> when the dependency is not registered and is not a built-in service.
/// </returns>
internal delegate Func<DefaultServiceScopeProvider, object?>? ChildResolverFactory(Type dependencyType, object? dependencyKey);

/// <inheritdoc cref="IServiceContainer"/>
public partial class ServiceContainer : IServiceContainer
{
    // concurrencyLevel: 1 sizes the internal lock array to a single lock instead of Environment.ProcessorCount
    // (~1.4 KB saved per dictionary). The descriptor map is written only under _lock, and the resolver caches
    // build outside any lock then publish via the indexer, so a single write lock is sufficient; reads are lock-free.
    // No comparer is passed: for a struct key the default comparer is devirtualized and inlined by the JIT, which a
    // custom IEqualityComparer<ServiceIdentifier> instance prevents (ServiceIdentifier implements IEquatable, so the
    // default comparer never boxes).
    private ConcurrentDictionary<ServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = new(concurrencyLevel: 1, capacity: 31);

    // Realized-resolver caches. The dominant null-key case is keyed by Type directly (skips ServiceIdentifier
    // construction + hash on the warm path); keyed services use the ServiceIdentifier cache. Invalidation swaps
    // both fields atomically (see InvalidateResolverCaches); reads capture the field once so a concurrent swap
    // is a benign "just-missed" window. `volatile` gives the swap acquire/release semantics.
    private volatile ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> _nullKeyResolvers = new(concurrencyLevel: 1, capacity: 31);
    private volatile ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> _keyedResolvers = new(concurrencyLevel: 1, capacity: 31);

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
    /// <param name="serviceFactory">The factory that constructs service instances, or <see langword="null"/> to use a <see cref="DefaultServiceFactory"/>.</param>
    /// <param name="options">The options for the container.</param>
    public ServiceContainer(IServiceFactory serviceFactory, ServiceContainerOptions options)
    {
        ServiceFactory = serviceFactory ?? new DefaultServiceFactory(this);
        Options = options;

        _serviceScopeFactory = new DefaultServiceScopeFactory(this);
        _rootScope = new(this, isRootScope: true);
    }

    /// <summary>
    /// Marks the container as disposed if it was not already, reporting whether disposal had already occurred.
    /// </summary>
    /// <returns><see langword="true"/> if the container was already disposed before this call; otherwise, <see langword="false"/>.</returns>
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
    /// <remarks>
    /// Disposes the instances the container created, in reverse creation order. Every instance is disposed even
    /// when some of them throw.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A tracked instance only implements <c>IAsyncDisposable</c>; use <c>DisposeAsync</c> instead. The other instances are still disposed.</exception>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose; the inner exceptions are in disposal order. A single failure is rethrown unchanged.</exception>
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
    /// <remarks>
    /// Disposes the instances the container created, in reverse creation order. Every instance is disposed even
    /// when some of them throw.
    /// </remarks>
    /// <exception cref="AggregateException">More than one tracked instance failed to dispose; the inner exceptions are in disposal order. A single failure is rethrown unchanged.</exception>
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
    /// Looks up the service descriptor for an already-constructed <see cref="ServiceIdentifier"/>, resolving a
    /// closed generic from a matching open-generic registration when present.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service to look up.</param>
    /// <returns>The matching <see cref="IServiceDescriptor"/>, or <see langword="null"/> if no registration matches.</returns>
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
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The container has been disposed.</exception>
    /// <exception cref="ServiceTypeNotRegistered">A required dependency of the service is not registered.</exception>
    /// <exception cref="CircularDependencyException">The service depends on itself through its dependencies.</exception>
    /// <exception cref="ServiceScopeRequiredException">The service is scoped and <see cref="ValidateScopes"/> is enabled.</exception>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        return GetKeyedService(serviceType, _rootScope, serviceKey: null);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The container has been disposed.</exception>
    /// <exception cref="ServiceTypeNotRegistered">A required dependency of the service is not registered.</exception>
    /// <exception cref="CircularDependencyException">The service depends on itself through its dependencies.</exception>
    /// <exception cref="ServiceScopeRequiredException">The service is scoped and <see cref="ValidateScopes"/> is enabled.</exception>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        return GetKeyedService(serviceType, _rootScope, serviceKey);
    }

    /// <summary>
    /// Resolves a service in <paramref name="scope"/>. The single resolve entry point behind every public
    /// <c>GetService</c>/<c>GetKeyedService</c>; callers validate their arguments, this checks container disposal.
    /// </summary>
    /// <param name="serviceType">The service type to resolve; never <see langword="null"/>.</param>
    /// <param name="scope">The scope the service is resolved in; never <see langword="null"/>.</param>
    /// <param name="serviceKey">The optional service key, or <see langword="null"/> for an unkeyed service.</param>
    /// <returns>The resolved instance, or <see langword="null"/> if the service is not registered.</returns>
    /// <exception cref="ObjectDisposedException">The container has been disposed.</exception>
    internal object? GetKeyedService(Type serviceType, DefaultServiceScopeProvider scope, object? serviceKey)
    {
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
    /// Resolves an unkeyed service, building and caching its resolver on first use, and returns the resulting
    /// instance, or <see langword="null"/> when the service is unregistered and not a built-in.
    /// </summary>
    /// <param name="serviceType">The unkeyed service type to resolve.</param>
    /// <param name="scope">The scope the instance is resolved in.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services to read from and populate.</param>
    /// <returns>The resolved service instance, or <see langword="null"/> if it is unregistered and not a built-in service.</returns>
    private object? BuildAndCacheNullKey(Type serviceType, DefaultServiceScopeProvider scope, ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache)
    {
        var keyedCache = _keyedResolvers;
        var resolver = GetOrBuildResolver(serviceType, serviceKey: null, nullKeyCache, keyedCache, new List<ServiceIdentifier>());

        // Unregistered, non-built-in → GetService returns null (only GetRequiredService throws). Not cached, to
        // keep the cache bounded by registered/dependency identifiers (a later Register rebuilds on next resolve).
        return resolver is null ? null : resolver(scope);
    }

    /// <summary>
    /// Resolves a keyed service, building and caching its resolver on first use, and returns the resulting
    /// instance, or <see langword="null"/> when the service is unregistered.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier (type and key) of the service to resolve.</param>
    /// <param name="scope">The scope the instance is resolved in.</param>
    /// <param name="keyedCache">The resolver cache for keyed services to read from and populate.</param>
    /// <returns>The resolved service instance, or <see langword="null"/> if it is unregistered.</returns>
    private object? BuildAndCacheKeyed(ServiceIdentifier serviceIdentifier, DefaultServiceScopeProvider scope, ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache)
    {
        var nullKeyCache = _nullKeyResolvers;
        var resolver = GetOrBuildResolver(serviceIdentifier.ServiceType, serviceIdentifier.ServiceKey, nullKeyCache, keyedCache, new List<ServiceIdentifier>());
        return resolver is null ? null : resolver(scope);
    }

    /// <summary>
    /// Builds the resolver for a built-in service: the container itself, the resolving scope, the scope factory or
    /// the service factory.
    /// </summary>
    /// <param name="serviceType">The built-in service type; <see cref="IsBuiltInService"/> must be <see langword="true"/> for it.</param>
    /// <returns>The resolver producing the built-in service.</returns>
    /// <exception cref="NotImplementedException"><paramref name="serviceType"/> is not a built-in service.</exception>
    private Func<DefaultServiceScopeProvider, object?> BuildBuiltInResolver(Type serviceType)
    {
        // Mapping the type to its built-in once here keeps type checks off the warm path (for example the
        // IServiceScopeFactory lookup behind every CreateScope).
        if (typeof(IServiceContainer).IsAssignableFrom(serviceType)
            || typeof(IServiceRegistry).IsAssignableFrom(serviceType)
            || typeof(IServiceDescriptorReceiver).IsAssignableFrom(serviceType))
        {
            return _ => this;
        }

        if (typeof(IServiceProvider).IsAssignableFrom(serviceType) || typeof(IScope).IsAssignableFrom(serviceType))
            return static scope => scope;

        if (typeof(IServiceScopeFactory).IsAssignableFrom(serviceType))
        {
            var serviceScopeFactory = _serviceScopeFactory;
            return _ => serviceScopeFactory;
        }

        if (typeof(IServiceFactory).IsAssignableFrom(serviceType))
        {
            var serviceFactory = ServiceFactory;
            return _ => serviceFactory;
        }

        throw new NotImplementedException($"Built-in service of type '{serviceType.FullName}' is not implemented.");
    }

    /// <summary>
    /// Caches, per <see cref="Type"/>, whether the type is a built-in service provided by the container.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> s_BuiltInServiceCache = new();

    /// <summary>
    /// Determines whether <paramref name="serviceType"/> is provided by the container itself rather than by a registration.
    /// </summary>
    /// <param name="serviceType">The service type to test.</param>
    /// <returns><see langword="true"/> if the type is a built-in service; otherwise, <see langword="false"/>.</returns>
    private static bool IsBuiltInService(Type serviceType)
    {
        return s_BuiltInServiceCache.GetOrAdd(serviceType, static type =>
            typeof(IServiceContainer).IsAssignableFrom(type)
            || typeof(IServiceRegistry).IsAssignableFrom(type)
            || typeof(IServiceDescriptorReceiver).IsAssignableFrom(type)
            || typeof(IServiceProvider).IsAssignableFrom(type)
            || typeof(IScope).IsAssignableFrom(type)
            || typeof(IServiceScopeFactory).IsAssignableFrom(type)
            || typeof(IServiceFactory).IsAssignableFrom(type));
    }

    /// <summary>
    /// Returns the resolver for the given service, building and caching it on a cache miss.
    /// </summary>
    /// <param name="serviceType">The service type to resolve a resolver for.</param>
    /// <param name="serviceKey">The optional service key, or <see langword="null"/> for an unkeyed service.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services to read from and populate.</param>
    /// <param name="keyedCache">The resolver cache for keyed services to read from and populate.</param>
    /// <param name="buildPath">The chain of service identifiers currently being built, used to detect circular dependencies.</param>
    /// <returns>The resolver for the service, or <see langword="null"/> if it is unregistered and not a built-in service.</returns>
    /// <exception cref="CircularDependencyException">A circular dependency is detected while building the resolver.</exception>
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
            resolver = BuildBuiltInResolver(serviceType);
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

    /// <summary>
    /// Builds the service types of a detected cycle, from the root of the build to the type that closes it.
    /// </summary>
    /// <param name="buildPath">The identifiers currently being built.</param>
    /// <param name="closing">The service type that was reached again.</param>
    /// <returns>The cycle path for <see cref="CircularDependencyException"/>.</returns>
    private static IReadOnlyList<Type> BuildCyclePath(List<ServiceIdentifier> buildPath, Type closing)
    {
        var path = new List<Type>(buildPath.Count + 1);
        for (int i = 0; i < buildPath.Count; i++)
            path.Add(buildPath[i].ServiceType);
        path.Add(closing);
        return path;
    }

    /// <summary>
    /// Wraps the construction delegate for a service with the behavior required by its
    /// <see cref="IServiceDescriptor.Lifetime"/> (transient, scoped, or singleton caching and disposal tracking).
    /// </summary>
    /// <param name="serviceIdentifier">The identifier (type and key) of the service.</param>
    /// <param name="serviceDescriptor">The descriptor describing the service's lifetime and implementation.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services.</param>
    /// <param name="keyedCache">The resolver cache for keyed services.</param>
    /// <param name="buildPath">The chain of service identifiers currently being built, used to detect circular dependencies.</param>
    /// <returns>A resolver that applies the service's lifetime behavior to the constructed instance.</returns>
    private Func<DefaultServiceScopeProvider, object?> BuildLifetimeResolver(
        ServiceIdentifier serviceIdentifier,
        IServiceDescriptor serviceDescriptor,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        List<ServiceIdentifier> buildPath)
    {
        // A pre-set singleton instance (e.g. RegisterSingleton<T>(instance)) needs NO construction, so its
        // ImplementationType is typically the service interface, so building a construct would fail. Return the
        // constant directly; the instance is never constructed and never tracked for disposal.
        if (serviceDescriptor.Lifetime == ServiceLifetime.Singleton && serviceDescriptor.SingletonInstance != null)
        {
            object preset = serviceDescriptor.SingletonInstance;
            return _ => preset;
        }

        ResolverNode node;
        switch (serviceDescriptor.Lifetime)
        {
            case ServiceLifetime.Transient:
                node = new TransientResolverNode();
                break;

            case ServiceLifetime.Scoped:
                // ValidateScopes is fixed for the container's life (Options is get-only), so capture it once.
                node = new ScopedResolverNode(serviceIdentifier, ValidateScopes);
                break;

            case ServiceLifetime.Singleton:
                node = new SingletonResolverNode(this, serviceDescriptor, RebakeFor(serviceIdentifier, nullKeyCache, keyedCache));
                break;

            default:
                var lifetime = serviceDescriptor.Lifetime;
                return _ => ThrowHelper.ThrowServiceLifetimeNotImplemented(lifetime);
        }

        // A singleton is constructed once, so compiling its construct delegate could never pay off.
        bool tiered = serviceDescriptor.Lifetime != ServiceLifetime.Singleton;
        node.Construct = BuildConstruct(serviceIdentifier, serviceDescriptor, nullKeyCache, keyedCache, buildPath, node, tiered);
        return node.Resolve;
    }

    /// <summary>
    /// Builds the delegate that constructs a fresh, non-<see langword="null"/> instance of the service, using its
    /// custom <see cref="IServiceDescriptor.InstanceFactory"/> when present, otherwise the configured
    /// <see cref="IServiceFactory"/>.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier (type and key) of the service.</param>
    /// <param name="serviceDescriptor">The descriptor describing the service's implementation.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services.</param>
    /// <param name="keyedCache">The resolver cache for keyed services.</param>
    /// <param name="buildPath">The chain of service identifiers currently being built, used to detect circular dependencies.</param>
    /// <param name="owner">The resolver node the construct delegate belongs to; receives the compiled delegate on tier-up and the disposal-tracking mode.</param>
    /// <param name="tiered"><see langword="true"/> to compile the node once it has been invoked often enough; <see langword="false"/> to stay interpreted.</param>
    /// <returns>A delegate that constructs a new instance of the service for a given scope.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Calls IServiceFactory.CreateInstance ([RequiresDynamicCode]). Reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresDynamicCode].")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Calls IServiceFactory.CreateInstance ([RequiresUnreferencedCode]); implementation members are preserved via the descriptor's annotated ImplementationType. Reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresUnreferencedCode].")]
    private Func<DefaultServiceScopeProvider, object> BuildConstruct(
        ServiceIdentifier serviceIdentifier,
        IServiceDescriptor serviceDescriptor,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        List<ServiceIdentifier> buildPath,
        ResolverNode owner,
        bool tiered)
    {
        var serviceKey = serviceIdentifier.ServiceKey;
        var closedGenericArguments = GetClosedGenericTypeArguments(serviceDescriptor);

        // Custom factory descriptor: call it live (not compiled), passing the threaded scope's provider so
        // scoped/keyed lookups inside the factory resolve in the correct scope. Preserve the legacy
        // `factory ?? CreateInstance` fall-through for a factory that returns null.
        var instanceFactory = serviceDescriptor.InstanceFactory;
        if (instanceFactory != null)
        {
            owner.Tracking = DisposalTracking.Check;
            return scope => instanceFactory.Invoke(scope.ServiceProvider, serviceKey)
                ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, closedGenericArguments);
        }

        if (ServiceFactory is DefaultServiceFactory defaultFactory)
        {
            // Tier 1 (interpreted): reflection-based construct whose children are this generation's resolvers,
            // built now so cycles and missing registrations surface on first resolve, exactly as before.
            var interpreted = defaultFactory.BuildInterpretedNode(
                serviceDescriptor.ImplementationType,
                closedGenericArguments,
                (dependencyType, dependencyKey) => GetOrBuildResolver(dependencyType, dependencyKey, nullKeyCache, keyedCache, buildPath),
                out var closedImplementationType);

            // The construct delegate always produces exactly the closed implementation type, so disposal tracking
            // is decided once instead of type-testing every instance.
            owner.Tracking = TypeHelper.IsDisposableType(closedImplementationType) ? DisposalTracking.Always : DisposalTracking.Never;

            if (!tiered || !RuntimeCompilation.IsSupported)
                return interpreted;

            // Inlining depth is fixed by the generation this node was built in (Freeze swaps the generation).
            int maxInlineDepth = _frozen ? -1 : 1;

            // Tier 2 (compiled): taken over on a later invocation; see TieredConstruct.
            return new TieredConstruct(
                owner,
                interpreted,
                () => CompileConstruct(defaultFactory, serviceIdentifier, serviceDescriptor, closedGenericArguments, nullKeyCache, keyedCache, maxInlineDepth)).Invoke;
        }

        // Custom IServiceFactory: fall back to by-type construction (children re-dispatch through GetKeyedService).
        owner.Tracking = DisposalTracking.Check;
        return scope => ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, closedGenericArguments);
    }

    /// <summary>
    /// Compiles the construct delegate of a node that has been invoked often enough: a node that invokes its
    /// children's resolvers directly and inlines simple-transient children.
    /// </summary>
    /// <param name="defaultFactory">The default service factory that compiles the node.</param>
    /// <param name="serviceIdentifier">The identifier (type and key) of the service.</param>
    /// <param name="serviceDescriptor">The descriptor describing the service's implementation.</param>
    /// <param name="closedGenericArguments">The generic type arguments closing an open-generic implementation, or <see langword="null"/>.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services of the generation the node was built in.</param>
    /// <param name="keyedCache">The resolver cache for keyed services of the generation the node was built in.</param>
    /// <param name="maxInlineDepth">The inlining depth: -1 (frozen) inlines the whole transient subtree, 1 (mutable) a single level.</param>
    /// <returns>The compiled construct delegate.</returns>
    private Func<DefaultServiceScopeProvider, object> CompileConstruct(
        DefaultServiceFactory defaultFactory,
        ServiceIdentifier serviceIdentifier,
        IServiceDescriptor serviceDescriptor,
        Type[]? closedGenericArguments,
        ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> nullKeyCache,
        ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> keyedCache,
        int maxInlineDepth)
    {
        // A private build path: compilation runs during a resolve, possibly on several threads at once, long after
        // the build that created the node. The node's children were built (and cycle-checked) with it, so the
        // path only serves the inlining cycle backstop.
        var buildPath = new List<ServiceIdentifier> { serviceIdentifier };

        // Inline simple-transient children's construction directly into this node, removing the per-child
        // delegate hop. Frozen mode inlines the whole transient subtree (maxInlineDepth -1). Mutable mode
        // inlines a single level (maxInlineDepth 1): a registration change swaps the entire resolver
        // generation (see InvalidateResolverCaches), so the one-level-inlined parent is discarded and rebuilt
        // against the new registration. Inlining deeper in mutable mode would only enlarge the recompiled
        // delegate without changing correctness, so it is capped at one level.
        Func<Type, object?, Type?> inlineSelector =
            (dependencyType, dependencyKey) => InlinableDependencyType(dependencyType, dependencyKey, defaultFactory);

        return defaultFactory.CompileNode(
            serviceDescriptor.ImplementationType,
            closedGenericArguments,
            (dependencyType, dependencyKey) => GetOrBuildResolver(dependencyType, dependencyKey, nullKeyCache, keyedCache, buildPath),
            buildPath,
            inlineSelector,
            maxInlineDepth);
    }

    /// <summary>
    /// Determines whether a dependency may be constructed directly (inlined into the parent node), returning the
    /// closed implementation type to use when it is a simple transient, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="dependencyType">The dependency service type being evaluated.</param>
    /// <param name="dependencyKey">The optional service key of the dependency, or <see langword="null"/> for an unkeyed dependency.</param>
    /// <param name="defaultFactory">The default service factory used to validate the candidate implementation type.</param>
    /// <returns>The closed implementation type to construct directly, or <see langword="null"/> if the dependency must be resolved through its own resolver.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "MakeGenericType closes an open-generic implementation type the user registered (AOT consumers must register closed generic types). Reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresDynamicCode].")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "MakeGenericType closes the descriptor's already-registered ImplementationType, whose members are preserved via its [DynamicallyAccessedMembers] annotation.")]
    private Type? InlinableDependencyType(Type dependencyType, object? dependencyKey, DefaultServiceFactory defaultFactory)
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

    /// <summary>
    /// Creates an action that stores a resolver into the resolver cache that matches
    /// <paramref name="serviceIdentifier"/>.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier (type and key) of the singleton service.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services.</param>
    /// <param name="keyedCache">The resolver cache for keyed services.</param>
    /// <returns>An action that publishes the supplied resolver into the cache matching <paramref name="serviceIdentifier"/>.</returns>
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
    /// Returns the closed generic type arguments when the descriptor's implementation is an open generic
    /// definition that <see cref="IServiceFactory.CreateInstance"/> must close via
    /// <see cref="Type.MakeGenericType(Type[])"/>; otherwise returns <see langword="null"/>.
    /// </summary>
    /// <param name="serviceDescriptor">The descriptor whose implementation type is examined.</param>
    /// <returns>The closed generic type arguments, or <see langword="null"/> when the implementation is not an open generic definition.</returns>
    private static Type[]? GetClosedGenericTypeArguments(IServiceDescriptor serviceDescriptor)
    {
        return serviceDescriptor.ImplementationType.IsGenericTypeDefinition
            ? serviceDescriptor.ServiceType.GenericTypeArguments
            : null;
    }

    /// <summary>
    /// Invalidates all cached resolvers so that subsequent resolves rebuild against the current registrations.
    /// </summary>
    // Call only from Register / UnregisterServiceInternal under _lock; the closed-generic auto-cache in
    // GetOptionalServiceDescriptor must NOT call it.
    private void InvalidateResolverCaches()
    {
        _nullKeyResolvers = new(concurrencyLevel: 1, capacity: 31);
        _keyedResolvers = new(concurrencyLevel: 1, capacity: 31);
    }

    /// <summary>
    /// Gets the <see cref="IServiceFactory"/> used by the container to construct service instances.
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
    /// Gets a value indicating whether existing registrations are read-only: overwriting or unregistering a service
    /// throws <see cref="ServiceRegistryReadOnlyException"/>. Set by <see cref="ServiceContainerOptions.ReadOnly"/>.
    /// </summary>
    public bool AreRegisteredServicesReadOnly => (Options & ServiceContainerOptions.ReadOnly) == ServiceContainerOptions.ReadOnly;

    /// <summary>
    /// Gets a value indicating whether resolving a scoped service outside a scope (from the container's root scope)
    /// throws <see cref="ServiceScopeRequiredException"/>. Set by <see cref="ServiceContainerOptions.ValidateScopes"/>.
    /// </summary>
    public bool ValidateScopes => (Options & ServiceContainerOptions.ValidateScopes) == ServiceContainerOptions.ValidateScopes;

    /// <summary>
    /// Gets a value indicating whether the container has been frozen via <see cref="Freeze"/>. A frozen
    /// container rejects further registration changes.
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