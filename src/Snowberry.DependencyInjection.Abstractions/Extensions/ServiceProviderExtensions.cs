using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Helper;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="IServiceProvider"/> type.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Gets a service of the specified type from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="serviceType">The type of the service to retrieve.</param>
    /// <returns>The service instance of the specified type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="serviceType"/> is null.</exception>
    /// <exception cref="ServiceTypeNotRegistered">Thrown when the service type is not registered in the container.</exception>
    public static object GetRequiredService(this IServiceProvider serviceProvider, Type serviceType)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return serviceProvider.GetService(serviceType) is object result ? result : throw new ServiceTypeNotRegistered(serviceType);
    }

    /// <summary>
    /// Gets a keyed service of the specified type from the <see cref="IKeyedServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The keyed service provider.</param>
    /// <param name="serviceType">The type of the service to retrieve.</param>
    /// <param name="serviceKey">The key of the service to retrieve.</param>
    /// <returns>The service instance of the specified type with the specified key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="serviceType"/> is null.</exception>
    /// <exception cref="ServiceTypeNotRegistered">Thrown when the service type with the specified key is not registered in the container.</exception>
    public static object GetRequiredKeyedService(this IKeyedServiceProvider serviceProvider, Type serviceType, object? serviceKey)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return serviceProvider.GetKeyedService(serviceType, serviceKey: serviceKey) is object result ? result : throw new ServiceTypeNotRegistered(serviceType);
    }

    /// <summary>
    /// Gets an optional service of the specified type from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The service instance of type <typeparamref name="T"/> if registered; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the service implementation cannot be cast to the requested type.</exception>
    public static T? GetService<T>(this IServiceProvider serviceProvider)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        object? service = serviceProvider.GetService(typeof(T));

        if (service is T typed)
            return typed;

        if (service is not null)
            ThrowHelper.ThrowInvalidServiceImplementationCast(typeof(T), service.GetType());

        return default;
    }

    /// <summary>
    /// Gets an optional keyed service of the specified type from the <see cref="IKeyedServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <param name="keyedServiceProvider">The keyed service provider.</param>
    /// <param name="serviceKey">The key of the service to retrieve.</param>
    /// <returns>The service instance of type <typeparamref name="T"/> with the specified key if registered; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyedServiceProvider"/> is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the service implementation cannot be cast to the requested type.</exception>
    public static T? GetKeyedService<T>(this IKeyedServiceProvider keyedServiceProvider, object? serviceKey)
    {
        _ = keyedServiceProvider ?? throw new ArgumentNullException(nameof(keyedServiceProvider));

        object? service = keyedServiceProvider.GetKeyedService(typeof(T), serviceKey: serviceKey);

        if (service is T typed)
            return typed;

        if (service is not null)
            ThrowHelper.ThrowInvalidServiceImplementationCast(typeof(T), service.GetType());

        return default;
    }

    /// <summary>
    /// Gets a required service of the specified type from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The service instance of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
    /// <exception cref="ServiceTypeNotRegistered">Thrown when the service type is not registered in the container.</exception>
    public static T GetRequiredService<T>(this IServiceProvider serviceProvider)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        if (serviceProvider.GetService<T>() is T typed)
            return typed;

        throw new ServiceTypeNotRegistered(typeof(T));
    }

    /// <summary>
    /// Gets a required keyed service of the specified type from the <see cref="IKeyedServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <param name="keyedServiceProvider">The keyed service provider.</param>
    /// <param name="serviceKey">The key of the service to retrieve.</param>
    /// <returns>The service instance of type <typeparamref name="T"/> with the specified key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyedServiceProvider"/> is null.</exception>
    /// <exception cref="ServiceTypeNotRegistered">Thrown when the service type with the specified key is not registered in the container.</exception>
    public static T GetRequiredKeyedService<T>(this IKeyedServiceProvider keyedServiceProvider, object? serviceKey)
    {
        _ = keyedServiceProvider ?? throw new ArgumentNullException(nameof(keyedServiceProvider));

        if (keyedServiceProvider.GetKeyedService<T>(serviceKey: serviceKey) is T typed)
            return typed;

        throw new ServiceTypeNotRegistered(typeof(T));
    }
}
