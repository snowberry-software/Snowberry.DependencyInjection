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
    /// Internal method to unregister service. Must be called within a write lock.
    /// </summary>
    private ServiceContainer UnregisterServiceInternal(Type serviceType, object? serviceKey, out bool successful)
    {
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

            successful = true;
            return this;
        }

        successful = false;
        return this;
    }
}
