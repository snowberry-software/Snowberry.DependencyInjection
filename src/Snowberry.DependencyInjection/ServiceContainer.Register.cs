using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;

namespace Snowberry.DependencyInjection;

public partial class ServiceContainer
{
    /// <inheritdoc/>
    public bool IsServiceRegistered(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
        return _serviceDescriptorMapping.ContainsKey(serviceIdentifier);
    }

    /// <inheritdoc/>
    public bool IsServiceRegistered<T>(object? serviceKey)
    {
        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        return IsServiceRegistered(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public IServiceRegistry Register(IServiceDescriptor serviceDescriptor, object? serviceKey = null)
    {
        _ = serviceDescriptor ?? throw new ArgumentNullException(nameof(serviceDescriptor));

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        if (_frozen)
            throw new ServiceRegistryReadOnlyException("The container is frozen; registrations cannot be added, removed, or overwritten.");

        if (serviceDescriptor.SingletonInstance != null && serviceDescriptor.Lifetime != ServiceLifetime.Singleton)
            throw new ArgumentException("Singleton can't be used in non-singleton lifetime!", nameof(serviceDescriptor));

        if (serviceDescriptor.SingletonInstance != null && serviceDescriptor.InstanceFactory != null)
            throw new ArgumentException("Singleton instance and instance factory can't be used together!", nameof(serviceDescriptor));

        lock (_lock)
        {
            var serviceIdentifier = new ServiceIdentifier(serviceDescriptor.ServiceType, serviceKey);
            bool foundExistingServiceDescriptor = _serviceDescriptorMapping.ContainsKey(serviceIdentifier);

            if (foundExistingServiceDescriptor && AreRegisteredServicesReadOnly)
                throw new ServiceRegistryReadOnlyException($"Service type '{serviceDescriptor.ServiceType.FullName}' is already registered!");

            if (foundExistingServiceDescriptor)
                UnregisterServiceInternal(serviceDescriptor.ServiceType, serviceKey, out _);

            _serviceDescriptorMapping.AddOrUpdate(serviceIdentifier, serviceDescriptor, (_, _) => serviceDescriptor);

            // The registration set changed → discard all compiled resolvers (they captured the old generation).
            InvalidateResolverCaches();
            return this;
        }
    }

    /// <inheritdoc/>
    public IServiceRegistry UnregisterService<T>(object? serviceKey, out bool successful)
    {
        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
        return UnregisterService(typeof(T), serviceKey, out successful);
    }

    /// <inheritdoc/>
    public IServiceRegistry UnregisterService(Type serviceType, object? serviceKey, out bool successful)
    {
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            return UnregisterServiceInternal(serviceType, serviceKey, out successful);
        }
    }

    /// <summary>
    /// Removes the service registration identified by <paramref name="serviceType"/> and
    /// <paramref name="serviceKey"/>, disposing the associated singleton instance if it is disposable.
    /// </summary>
    /// <param name="serviceType">The type of the service to unregister.</param>
    /// <param name="serviceKey">The optional key identifying the registration, or <see langword="null"/> for the default registration.</param>
    /// <param name="successful"><see langword="true"/> when a matching registration was found and removed; otherwise <see langword="false"/>.</param>
    /// <returns>The current <see cref="ServiceContainer"/> instance.</returns>
    /// <exception cref="ServiceRegistryReadOnlyException">The container is frozen or the service registry is read-only.</exception>
    // Must be called while holding _lock.
    private ServiceContainer UnregisterServiceInternal(Type serviceType, object? serviceKey, out bool successful)
    {
        if (_frozen)
            throw new ServiceRegistryReadOnlyException($"The container is frozen; '{serviceType.Name}' cannot be unregistered.");

        if (AreRegisteredServicesReadOnly)
            throw new ServiceRegistryReadOnlyException($"The service registry is read-only and does not allow unregistering services ('{serviceType.Name}')!");

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);

        if (_serviceDescriptorMapping.TryRemove(serviceIdentifier, out var serviceDescriptor))
        {
            if (serviceDescriptor.Lifetime is ServiceLifetime.Singleton && serviceDescriptor.SingletonInstance is IDisposable disposableSingleton)
            {
                _rootScope.DisposableContainer.RemoveDisposable(disposableSingleton);
                disposableSingleton.Dispose();
            }

            // The registration set changed → discard all compiled resolvers (they captured the old generation).
            InvalidateResolverCaches();

            successful = true;
            return this;
        }

        successful = false;
        return this;
    }
}
