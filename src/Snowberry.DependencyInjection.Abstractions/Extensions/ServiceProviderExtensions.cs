using System.Diagnostics.CodeAnalysis;
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
    /// Creates an instance of the specified type, optionally using the provided generic type parameters, and resolves its dependencies from the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve dependencies required by the constructed instance.</param>
    /// <param name="type">The type of object to create. Must have a public constructor and any required public properties.</param>
    /// <param name="genericTypeArguments">An array of types to use as generic type arguments if the specified type is a generic type definition; otherwise, null.</param>
    /// <returns>An instance of the specified type with dependencies resolved from the service provider.</returns>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    public static object CreateInstance(this IServiceProvider serviceProvider, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, Type[]? genericTypeArguments = null)
    {
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        return serviceFactory.CreateInstance(type, serviceProvider, genericTypeArguments);
    }

    /// <summary>
    /// Creates an instance of the specified generic type using the provided service provider, optionally supplying generic type parameters.
    /// </summary>
    /// <typeparam name="T">The type of object to create. Must have a public constructor and public properties accessible for initialization.</typeparam>
    /// <param name="serviceProvider">The service provider used to resolve dependencies and create the instance.</param>
    /// <param name="genericTypeArguments">An array of types to use as generic type arguments, or null to use the default type parameters.</param>
    /// <returns>An instance of type <typeparamref name="T"/> created with the specified generic type parameters and resolved dependencies.</returns>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    public static T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(this IServiceProvider serviceProvider, Type[]? genericTypeArguments = null)
    {
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();
        return serviceFactory.CreateInstance<T>(serviceProvider, genericTypeArguments);
    }

    /// <summary>
    /// Creates a new scope using the <see cref="IServiceScopeFactory"/> from the <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The sope.</returns>
    public static IScope CreateScope(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
    }

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
