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
    private ConcurrentDictionary<ServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = new(ServiceIdentifierComparer.s_Instance);

    // Realized-resolver caches. The dominant null-key case is keyed by Type directly (skips ServiceIdentifier
    // construction + hash on the warm path); keyed services use the ServiceIdentifier cache. Invalidation swaps
    // both fields atomically (see InvalidateResolverCaches); reads capture the field once so a concurrent swap
    // is a benign "just-missed" window. `volatile` gives the swap acquire/release semantics.
    private volatile ConcurrentDictionary<Type, Func<DefaultServiceScopeProvider, object?>> _nullKeyResolvers = new();
    private volatile ConcurrentDictionary<ServiceIdentifier, Func<DefaultServiceScopeProvider, object?>> _keyedResolvers = new(ServiceIdentifierComparer.s_Instance);

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
    /// Caches, per <see cref="Type"/>, whether the type is a built-in service provided by the container.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, bool> s_BuiltInServiceCache = new();

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
    /// Builds the delegate that constructs a fresh, non-<see langword="null"/> instance of the service, using its
    /// custom <see cref="IServiceDescriptor.InstanceFactory"/> when present, otherwise the configured
    /// <see cref="IServiceFactory"/>.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier (type and key) of the service.</param>
    /// <param name="serviceDescriptor">The descriptor describing the service's implementation.</param>
    /// <param name="nullKeyCache">The resolver cache for unkeyed services.</param>
    /// <param name="keyedCache">The resolver cache for keyed services.</param>
    /// <param name="buildPath">The chain of service identifiers currently being built, used to detect circular dependencies.</param>
    /// <returns>A delegate that constructs a new instance of the service for a given scope.</returns>
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
    /// Determines whether a dependency may be constructed directly while the container is frozen, returning the
    /// closed implementation type to use when it is a simple transient, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="dependencyType">The dependency service type being evaluated.</param>
    /// <param name="dependencyKey">The optional service key of the dependency, or <see langword="null"/> for an unkeyed dependency.</param>
    /// <param name="defaultFactory">The default service factory used to validate the candidate implementation type.</param>
    /// <returns>The closed implementation type to construct directly, or <see langword="null"/> if the dependency must be resolved through its own resolver.</returns>
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
        _nullKeyResolvers = new();
        _keyedResolvers = new(ServiceIdentifierComparer.s_Instance);
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
    /// Gets whether the registered services are read-only and can't be overwritten.
    /// </summary>
    public bool AreRegisteredServicesReadOnly => (Options & ServiceContainerOptions.ReadOnly) == ServiceContainerOptions.ReadOnly;

    /// <summary>
    /// Gets whether scope validation is enabled.
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