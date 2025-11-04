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
    private ConcurrentDictionary<IServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = [];

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
        // Fast path: try to read without any locking first (using ConcurrentDictionary's thread safety)
        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);

        if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out var serviceDescriptor))
        {
            DisposeThrowHelper.ThrowIfDisposed(IsDisposed, this);
            return serviceDescriptor;
        }

        // If not found and it's not a generic type, return null without locking
        if (serviceType.GenericTypeArguments.Length == 0)
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
            var genericServiceIdentifier = new ServiceIdentifier(genericType, serviceKey);

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

        var descriptor = GetOptionalServiceDescriptor(serviceType, serviceKey);

        if (descriptor == null)
            return null;

        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
        return GetInstanceFromDescriptor(serviceIdentifier, descriptor, scope);
    }

    private object? GetBuiltInService(Type serviceType, DefaultServiceScopeProvider scope, object? serviceKey)
    {
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

    private static bool IsBuiltInService(Type serviceType)
    {
        return typeof(IServiceProvider).IsAssignableFrom(serviceType)
            || typeof(IScope).IsAssignableFrom(serviceType)
            || typeof(IServiceScopeFactory).IsAssignableFrom(serviceType)
            || typeof(IServiceFactory).IsAssignableFrom(serviceType);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private object GetInstanceFromDescriptor(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, IScope scope)
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
                            serviceDescriptor.SingletonInstance = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceIdentifier.ServiceKey)
                                ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, serviceDescriptor.ServiceType.GenericTypeArguments);

                            if (serviceDescriptor.SingletonInstance.IsDisposable())
                                RootScope.DisposableContainer.RegisterDisposable(serviceDescriptor.SingletonInstance);
                        }
                    }
                }

                return serviceDescriptor.SingletonInstance;

            case ServiceLifetime.Transient:

                object? transientInstance = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceIdentifier.ServiceKey)
                    ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, serviceDescriptor.ServiceType.GenericTypeArguments);

                if (transientInstance.IsDisposable())
                    scope.DisposableContainer.RegisterDisposable(transientInstance);

                return transientInstance;

            case ServiceLifetime.Scoped:
                {
                    bool resolved = scope.TryGetScopedInstance(serviceIdentifier, out object? instance);

                    if (resolved)
                        return instance!;

                    lock (_lock)
                    {
                        if ((resolved = scope.TryGetScopedInstance(serviceIdentifier, out instance)) == false)
                        {
                            instance = serviceDescriptor.InstanceFactory?.Invoke(scope.ServiceProvider, serviceIdentifier.ServiceKey)
                                ?? ServiceFactory.CreateInstance(serviceDescriptor.ImplementationType, scope.ServiceProvider, serviceDescriptor.ServiceType.GenericTypeArguments);

                            if (instance.IsDisposable())
                                scope.DisposableContainer.RegisterDisposable(instance);

                            scope.AddCached(serviceIdentifier, instance);
                        }
                    }

                    return instance!;
                }

            default:
                return ThrowHelper.ThrowServiceLifetimeNotImplemented(serviceDescriptor.Lifetime);
        }
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
    /// Returns whether the registered services are read-only and can't be overwritten.
    /// </summary>
    public bool AreRegisteredServicesReadOnly => (Options & ServiceContainerOptions.ReadOnly) == ServiceContainerOptions.ReadOnly;

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