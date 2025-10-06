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
    public static IServiceRegistry RegisterSingleton<T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: null);
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
    public static IServiceRegistry RegisterSingleton<T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: instanceFactory);
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
    public static IServiceRegistry RegisterSingleton<T>(this IServiceRegistry serviceRegistry, T instance, object? serviceKey = null)
    {
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: instance,
            instanceFactory: null);
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
    public static IServiceRegistry RegisterSingleton<T, TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: null);
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
    public static IServiceRegistry RegisterSingleton<T, TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
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
    public static IServiceRegistry RegisterSingleton<T, TImpl>(this IServiceRegistry serviceRegistry, TImpl instance, object? serviceKey = null) where TImpl : T
    {
        _ = instance ?? throw new ArgumentNullException(nameof(instance));

        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Singleton,
            instance);
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
    public static IServiceRegistry RegisterTransient<T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        return serviceRegistry.Register(
           typeof(T),
           typeof(T),
           serviceKey,
           ServiceLifetime.Transient,
           null);
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
    public static IServiceRegistry RegisterTransient<T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null,
            instanceFactory: instanceFactory);
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
    public static IServiceRegistry RegisterTransient<T, TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null);
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
    public static IServiceRegistry RegisterTransient<T, TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Transient,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
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
    public static IServiceRegistry RegisterScoped<T>(this IServiceRegistry serviceRegistry, object? serviceKey = null)
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null);
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
    public static IServiceRegistry RegisterScoped<T>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory instanceFactory, object? serviceKey = null)
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(T),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null,
            instanceFactory: instanceFactory);
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
    public static IServiceRegistry RegisterScoped<T, TImpl>(this IServiceRegistry serviceRegistry, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null);
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
    public static IServiceRegistry RegisterScoped<T, TImpl>(this IServiceRegistry serviceRegistry, ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T
    {
        return serviceRegistry.Register(
            typeof(T),
            typeof(TImpl),
            serviceKey,
            ServiceLifetime.Scoped,
            singletonInstance: null,
            instanceFactory: (serviceProvider, serviceKey) => instanceFactory(serviceProvider, serviceKey)!);
    }
}
