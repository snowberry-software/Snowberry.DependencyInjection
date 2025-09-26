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

        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        lock (_lock)
        {
            var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
            return _serviceDescriptorMapping.ContainsKey(serviceIdentifier);
        }
    }

    /// <inheritdoc/>
    public bool IsServiceRegistered<T>(object? serviceKey)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return IsServiceRegistered(typeof(T), serviceKey);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: instanceFactory);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T>(object? serviceKey = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T>(T instance, object? serviceKey = null)
    {
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: instance,
            instanceFactory: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T, TImpl>(object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterSingleton<T, TImpl>(TImpl instance, object? serviceKey = null) where TImpl : T
    {
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            instance);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterTransient<T>(object? serviceKey = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Transient,
            null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterTransient<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null,
            instanceFactory: instanceFactory);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterTransient<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterTransient<T, TImpl>(object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterScoped<T>(object? serviceKey = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterScoped<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null,
            instanceFactory: instanceFactory);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterScoped<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
    }

    /// <inheritdoc/>
    public IServiceRegistry RegisterScoped<T, TImpl>(object? serviceKey = null) where TImpl : T
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        return Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry Register(
        Type serviceType,
        Type implementationType,
        object? serviceKey,
        ServiceLifetime lifetime,
        object? singletonInstance)
    {
        return Register(
            serviceType: serviceType,
            implementationType: implementationType,
            serviceKey: serviceKey,
            lifetime: lifetime,
            singletonInstance: singletonInstance,
            instanceFactory: null);
    }

    /// <inheritdoc/>
    public IServiceRegistry Register(
        Type serviceType,
        Type implementationType,
        object? serviceKey,
        ServiceLifetime lifetime,
        object? singletonInstance,
        ServiceInstanceFactory? instanceFactory)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = implementationType ?? throw new ArgumentNullException(nameof(implementationType));

        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ServiceContainer));

        if (singletonInstance != null && lifetime != ServiceLifetime.Singleton)
            throw new ArgumentException("Singleton can't be used in non-singleton lifetime!", nameof(singletonInstance));

        if (singletonInstance != null && instanceFactory != null)
            throw new ArgumentException("Singleton instance and instance factory can't be used together!", nameof(singletonInstance));

        lock (_lock)
        {
            var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);
            bool foundExistingServiceDescriptor = _serviceDescriptorMapping.ContainsKey(serviceIdentifier);

            var newDescriptor = lifetime switch
            {
                ServiceLifetime.Singleton => ServiceDescriptor.Singleton(serviceType, implementationType, singletonInstance),
                ServiceLifetime.Scoped => ServiceDescriptor.Scoped(serviceType, implementationType),
                ServiceLifetime.Transient => ServiceDescriptor.Transient(serviceType, implementationType),
                _ => ThrowHelper.ThrowServiceLifetimeNotImplemented(lifetime) as IServiceDescriptor
            };

            newDescriptor!.InstanceFactory = instanceFactory;

            return Register(newDescriptor, serviceKey: serviceKey);
        }
    }

    /// <inheritdoc/>
    public IServiceRegistry Register(IServiceDescriptor serviceDescriptor, object? serviceKey = null)
    {
        _ = serviceDescriptor ?? throw new ArgumentNullException(nameof(serviceDescriptor));

        lock (_lock)
        {
            var serviceIdentifier = new ServiceIdentifier(serviceDescriptor.ServiceType, serviceKey);
            bool foundExistingServiceDescriptor = _serviceDescriptorMapping.ContainsKey(serviceIdentifier);

            if (foundExistingServiceDescriptor && AreRegisteredServicesReadOnly)
                throw new ServiceRegistryReadOnlyException($"Service type '{serviceDescriptor.ServiceType.FullName}' is already registered!");

            if (foundExistingServiceDescriptor)
                UnregisterService(serviceDescriptor.ServiceType, serviceKey, out _);

            _serviceDescriptorMapping.AddOrUpdate(serviceIdentifier, serviceDescriptor, (_, _) => serviceDescriptor);
            return this;
        }
    }

    /// <inheritdoc/>
    public IServiceRegistry UnregisterService<T>(object? serviceKey, out bool successful)
    {
        return UnregisterService(typeof(T), serviceKey, out successful);
    }

    /// <inheritdoc/>
    public IServiceRegistry UnregisterService(Type serviceType, object? serviceKey, out bool successful)
    {
        lock (_lock)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (AreRegisteredServicesReadOnly)
                throw new ServiceRegistryReadOnlyException($"The service registry is read-only and does not allow unregistering services ('{serviceType.Name}')!");

            var serviceIdentifier = new ServiceIdentifier(serviceType, serviceKey);

            if (_serviceDescriptorMapping.TryRemove(serviceIdentifier, out var serviceDescriptor))
            {
                if (serviceDescriptor.Lifetime is ServiceLifetime.Singleton && serviceDescriptor.SingletonInstance is IDisposable disposableSingleton)
                {
                    _disposableContainer.Remove(disposableSingleton);
                    disposableSingleton.Dispose();
                }

                successful = true;
                return this;
            }
        }

        successful = false;
        return this;
    }
}
