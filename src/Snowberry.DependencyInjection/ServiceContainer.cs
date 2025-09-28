using System.Collections.Concurrent;
using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Lookup;

namespace Snowberry.DependencyInjection;

public partial class ServiceContainer : IServiceContainer
{
    private ConcurrentDictionary<IServiceIdentifier, IServiceDescriptor> _serviceDescriptorMapping = [];
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private volatile bool _isDisposed;
    private DisposableContainer _disposableContainer = new();

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
    public ServiceContainer(IScopedServiceFactory serviceFactory, ServiceContainerOptions options)
    {
        ServiceFactory = serviceFactory ?? new DefaultServiceFactory(this);
        Options = options;
    }

    /// <summary>
    /// Adds default services to the container.
    /// </summary>
    protected virtual void AddDefaultServices()
    {
    }

    /// <summary>
    /// Disposes the core.
    /// </summary>
    /// <returns>Whether the core is already disposed.</returns>
    private bool DisposeCore()
    {
        if (_isDisposed)
            return true;

        _lock.EnterWriteLock();
        try
        {
            if (_isDisposed)
                return true;

            _isDisposed = true;
            ServiceFactory.NotifyScopeDisposed(null);
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        _lock.EnterWriteLock();
        try
        {
            _disposableContainer.Dispose();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        ValueTask disposeTask;
        _lock.EnterWriteLock();
        try
        {
            disposeTask = _disposableContainer.DisposeAsync();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        await disposeTask.ConfigureAwait(false);

        _lock.Dispose();
    }
#endif

    /// <inheritdoc/>
    public void RegisterDisposable(IDisposable disposable)
    {
        RegisterDisposable((object)disposable);
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

        _lock.EnterWriteLock();
        try
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));

            _disposableContainer.RegisterDisposable(disposable);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public IServiceDescriptor? GetOptionalServiceDescriptor(Type serviceType, object? serviceKey)
    {
        // Fast path: try to read without any locking first (using ConcurrentDictionary's thread safety)
        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);

        if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out var serviceDescriptor))
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));

            return serviceDescriptor;
        }

        // If not found and it's not a generic type, return null without locking
        if (serviceType.GenericTypeArguments.Length == 0)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));

            return null;
        }

        // For generic types, we need to check for open generic registrations and potentially create new descriptors
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));

            // Double-check if the service was registered while we were waiting for the lock
            if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out serviceDescriptor))
                return serviceDescriptor;

            var genericType = serviceType.GetGenericTypeDefinition();
            var genericServiceIdentifier = new ServiceIdentifier(genericType, serviceKey);

            // Check if we have an open generic registration
            if (_serviceDescriptorMapping.TryGetValue(genericServiceIdentifier, out serviceDescriptor))
            {
                _lock.EnterWriteLock();
                try
                {
                    // Triple-check after acquiring write lock (other thread might have added it)
                    if (_serviceDescriptorMapping.TryGetValue(serviceIdentifier, out var existingDescriptor))
                        return existingDescriptor;

                    var newServiceDescriptor = serviceDescriptor.CloneFor(serviceType);
                    _serviceDescriptorMapping.AddOrUpdate(serviceIdentifier, newServiceDescriptor, (_, _) => newServiceDescriptor);
                    return newServiceDescriptor;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            return null;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <inheritdoc/>
    public IServiceDescriptor GetServiceDescriptor(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        var descriptor = GetOptionalServiceDescriptor(serviceType, serviceKey);

        return descriptor ?? throw new ServiceTypeNotRegistered(serviceType);
    }

    /// <inheritdoc/>
    public IServiceDescriptor GetServiceDescriptor<T>(object? serviceKey)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return GetServiceDescriptor(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public IServiceDescriptor? GetOptionalServiceDescriptor<T>(object? serviceKey)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return GetOptionalServiceDescriptor(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public object CreateInstance(Type type, Type[]? genericTypeParameters = null)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return ServiceFactory.CreateInstance(type, genericTypeParameters);
    }

    /// <inheritdoc/>
    public T CreateInstance<T>(Type[]? genericTypeParameters = null)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return ServiceFactory.CreateInstance<T>(genericTypeParameters);
    }

    /// <inheritdoc/>
    public IScope CreateScope()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        var scope = new Scope();
        scope.SetServiceFactory(new ScopeServiceFactory(scope, ServiceFactory));
        ServiceFactory.NotifyScopeCreated(scope);
        return scope;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return ServiceFactory.GetService(serviceType);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return ServiceFactory.GetKeyedService(serviceType, serviceKey);
    }

    /// <inheritdoc/>
    public ConstructorInfo? GetConstructor(Type instanceType)
    {
        return ServiceFactory.GetConstructor(instanceType);
    }

    /// <summary>
    /// The service factory that will be used.
    /// </summary>
    public IScopedServiceFactory ServiceFactory { get; }

    /// <inheritdoc/>
    public IServiceDescriptor[] GetServiceDescriptors()
    {
        _lock.EnterReadLock();
        try
        {
            return [.. _serviceDescriptorMapping.Values];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _serviceDescriptorMapping.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc/>
    public int DisposableCount
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _disposableContainer.DisposableCount;
            }
            finally
            {
                _lock.ExitReadLock();
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
    public bool IsDisposed => _isDisposed;
}