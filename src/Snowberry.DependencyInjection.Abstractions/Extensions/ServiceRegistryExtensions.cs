using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IServiceRegistry"/> type.
/// </summary>
public static class ServiceRegistryExtensions
{
    /// <summary>
    /// Tries to add the given <paramref name="serviceDescriptor"/> to the <paramref name="serviceRegistry"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceDescriptor">The service descriptor.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <returns>Whether the service has been added.</returns>
    public static bool TryAdd(this IServiceRegistry serviceRegistry, IServiceDescriptor serviceDescriptor, object? serviceKey = null)
    {
        if (serviceRegistry.IsServiceRegistered(serviceDescriptor.ServiceType, serviceKey: serviceKey))
            return false;

        serviceRegistry.Register(serviceDescriptor, serviceKey: serviceKey);
        return true;
    }

    /// <summary>
    /// Creates and registers a singleton service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a singleton service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Registers a singleton service of the type <typeparamref name="T"/> using the given <paramref name="instance"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instance">The instance that will be returned when requesting the <typeparamref name="T"/> service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/> using the given <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The instance that will be returned when requesting the <typeparamref name="T"/> service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a transient service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a transient service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a transient service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a transient service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a scoped service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a scoped service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a scoped service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
    /// Creates and registers a scoped service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceRegistry">The service registry.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
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
