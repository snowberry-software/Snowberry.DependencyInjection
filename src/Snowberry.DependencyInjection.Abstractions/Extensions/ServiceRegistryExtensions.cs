using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IServiceRegistry"/> type.
/// </summary>
public static class ServiceRegistryExtensions
{
    /// <summary>
    /// Attempts to register the specified service descriptor if the service type is not already registered.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceDescriptor">The service descriptor containing registration information.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="serviceDescriptor"/> is null.</exception>
    public static bool TryRegister(this IServiceRegistry serviceRegistry, IServiceDescriptor serviceDescriptor, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = serviceDescriptor ?? throw new ArgumentNullException(nameof(serviceDescriptor));

        if (serviceRegistry.IsServiceRegistered(serviceDescriptor.ServiceType, serviceKey: serviceKey))
            return false;

        serviceRegistry.Register(serviceDescriptor, serviceKey: serviceKey);
        return true;
    }

    /// <summary>
    /// Registers a singleton service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: null), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: null), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a singleton service of type <typeparamref name="T"/> using a factory function to create instances.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: null);
        descriptor.InstanceFactory = instanceFactory;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service of type <typeparamref name="T"/> using a factory function to create instances.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: null);
        descriptor.InstanceFactory = instanceFactory;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a singleton service of type <typeparamref name="T"/> using a pre-created instance.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instance">The pre-created instance that will be returned when the service is requested.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instance"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, T instance, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return serviceRegistry.Register(ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: instance), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service of type <typeparamref name="T"/> using a pre-created instance.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instance">The pre-created instance that will be returned when the service is requested.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instance"/> is null.</exception>
    public static bool TryRegisterSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, T instance, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return TryRegister(serviceRegistry, ServiceDescriptor.Singleton(typeof(T), typeof(T), singletonInstance: instance), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: null), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: null), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: null);
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: null);
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a pre-created instance.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instance">The pre-created instance of type <typeparamref name="TImpl"/> that will be returned when the service is requested.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instance"/> is null.</exception>
    public static IServiceRegistry RegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, TImpl instance, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return serviceRegistry.Register(ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: instance), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a singleton service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a pre-created instance.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instance">The pre-created instance of type <typeparamref name="TImpl"/> that will be returned when the service is requested.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instance"/> is null.</exception>
    public static bool TryRegisterSingleton<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, TImpl instance, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return TryRegister(serviceRegistry, ServiceDescriptor.Singleton(typeof(T), typeof(TImpl), singletonInstance: instance), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a transient service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Transient(typeof(T), typeof(T)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a transient service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Transient(typeof(T), typeof(T)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a transient service of type <typeparamref name="T"/> using a factory function to create instances.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Transient(typeof(T), typeof(T));
        descriptor.InstanceFactory = instanceFactory;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a transient service of type <typeparamref name="T"/> using a factory function to create instances.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Transient(typeof(T), typeof(T));
        descriptor.InstanceFactory = instanceFactory;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a transient service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterTransient<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Transient(typeof(T), typeof(TImpl)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a transient service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterTransient<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Transient(typeof(T), typeof(TImpl)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a transient service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterTransient<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Transient(typeof(T), typeof(TImpl));
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a transient service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// A new instance will be created each time the service is requested.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterTransient<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Transient(typeof(T), typeof(TImpl));
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a scoped service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Scoped(typeof(T), typeof(T)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a scoped service of type <typeparamref name="T"/> where the service type and implementation type are the same.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Scoped(typeof(T), typeof(T)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a scoped service of type <typeparamref name="T"/> using a factory function to create instances.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Scoped(typeof(T), typeof(T));
        descriptor.InstanceFactory = instanceFactory;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a scoped service of type <typeparamref name="T"/> using a factory function to create instances.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The type of the service to register.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterScoped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Scoped(typeof(T), typeof(T));
        descriptor.InstanceFactory = instanceFactory;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a scoped service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static IServiceRegistry RegisterScoped<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return serviceRegistry.Register(ServiceDescriptor.Scoped(typeof(T), typeof(TImpl)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a scoped service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> is null.</exception>
    public static bool TryRegisterScoped<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));

        return TryRegister(serviceRegistry, ServiceDescriptor.Scoped(typeof(T), typeof(TImpl)), serviceKey: serviceKey);
    }

    /// <summary>
    /// Registers a scoped service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static IServiceRegistry RegisterScoped<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Scoped(typeof(T), typeof(TImpl));
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return serviceRegistry.Register(descriptor, serviceKey: serviceKey);
    }

    /// <summary>
    /// Attempts to register a scoped service where the service type is <typeparamref name="T"/> and the implementation type is <typeparamref name="TImpl"/>, using a factory function to create instances.
    /// A single instance will be created per scope and reused within that scope.
    /// </summary>
    /// <param name="serviceRegistry">The service registry to register the service with.</param>
    /// <param name="instanceFactory">The factory function used to create service instances of type <typeparamref name="TImpl"/>.</param>
    /// <param name="serviceKey">The optional key to associate with the service registration.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    /// <typeparam name="TImpl">The implementation type that provides the service.</typeparam>
    /// <returns><c>true</c> if the service was successfully registered; <c>false</c> if the service type was already registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRegistry"/> or <paramref name="instanceFactory"/> is null.</exception>
    public static bool TryRegisterScoped<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        _ = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
        _ = instanceFactory ?? throw new ArgumentNullException(nameof(instanceFactory));

        var descriptor = ServiceDescriptor.Scoped(typeof(T), typeof(TImpl));
        descriptor.InstanceFactory = (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!;

        return TryRegister(serviceRegistry, descriptor, serviceKey: serviceKey);
    }
}
