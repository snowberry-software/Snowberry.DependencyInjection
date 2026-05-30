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

/// <inheritdoc cref="IServiceContainer"/>.
public partial class ServiceContainer : IServiceContainer
{
    private ConcurrentDictionary<ServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = new(ServiceIdentifierComparer.Instance);

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

        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
        return GetKeyedService(serviceType, RootScope, serviceKey: null);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
        return GetKeyedService(serviceType, RootScope, serviceKey);
    }

    internal object? GetKeyedService(Type serviceType, DefaultServiceScopeProvider scope, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = scope ?? throw new ArgumentNullException(nameof(scope));

        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);

        if (serviceKey == null && IsBuiltInService(serviceType))
            return GetBuiltInService(serviceType, scope, serviceKey);

        // Build the identifier once and thread it through both the descriptor lookup and the instance resolution.
        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
        var descriptor = GetOptionalServiceDescriptor(serviceIdentifier);

        if (descriptor == null)
            return null;

        if (ValidateScopes && descriptor.Lifetime == ServiceLifetime.Scoped && scope.IsGlobalScope)
            throw new ServiceScopeRequiredException(serviceType);

        return GetInstanceFromDescriptor(serviceIdentifier, descriptor, scope);
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

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private object GetInstanceFromDescriptor(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, DefaultServiceScopeProvider scope)
    {
        _ = serviceDescriptor ?? throw new ArgumentNullException(nameof(serviceDescriptor));
        _ = scope ?? throw new ArgumentNullException(nameof(scope));

        DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);

        switch (serviceDescriptor.Lifetime)
        {
            case ServiceLifetime.Singleton:

                // NOTE(VNC): Only register the disposable of the singleton if no explicit instance has been set before.
                if (serviceDescriptor.SingletonInstance == null)
                {
                    lock (_lock)
                    {
                        if (serviceDescriptor.SingletonInstance == null)
                        {
                            serviceDescriptor.SingletonInstance = serviceDescriptor.InstanceFactory?.Invoke(RootScope.ServiceProvider, serviceIdentifier.ServiceKey)
                                ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, RootScope.ServiceProvider, GetClosedGenericTypeArguments(serviceDescriptor));

                            if (serviceDescriptor.SingletonInstance.IsDisposable())
                                RootScope.TrackNewDisposable(serviceDescriptor.SingletonInstance);
                        }
                    }
                }

                return serviceDescriptor.SingletonInstance;

            case ServiceLifetime.Transient:

                object? transientInstance = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceIdentifier.ServiceKey)
                    ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, GetClosedGenericTypeArguments(serviceDescriptor));

                if (transientInstance.IsDisposable())
                    scope.TrackNewDisposable(transientInstance);

                return transientInstance;

            case ServiceLifetime.Scoped:
                {
                    if (scope.TryGetScopedInstance(serviceIdentifier, out object? instance))
                        return instance!;

                    // Construct OUTSIDE any lock. The container-wide lock is deliberately NOT taken here: two
                    // different scopes resolving scoped services must not serialize on it, and constructing
                    // without holding a lock prevents a nested dependency that needs the container lock (e.g. a
                    // singleton) from inverting lock order against another thread → no deadlock.
                    object created = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceIdentifier.ServiceKey)
                        ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, GetClosedGenericTypeArguments(serviceDescriptor));

                    // Publish under the per-scope lock. If another thread won the race, it returns its instance;
                    // ours becomes a redundant loser.
                    bool won = scope.TryAddScopedInstance(serviceIdentifier, created, out object? existing);

                    // Track the created instance for disposal whether we won or lost, so a disposable loser is
                    // disposed at scope teardown rather than leaked.
                    if (created.IsDisposable())
                        scope.TrackNewDisposable(created);

                    return won ? created : existing!;
                }

            default:
                return ThrowHelper.ThrowServiceLifetimeNotImplemented(serviceDescriptor.Lifetime);
        }
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