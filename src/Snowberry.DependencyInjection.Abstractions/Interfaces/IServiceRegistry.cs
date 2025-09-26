namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Represents a registry to register services.
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Registers the given service.
    /// </summary>
    /// <param name="serviceType">The service to register.</param>
    /// <param name="implementationType">The implementation type of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <param name="lifetime">The lifetime of the service.</param>
    /// <param name="singletonInstance">The optional singleton instance.</param>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry Register(Type serviceType, Type implementationType, object? serviceKey, ServiceLifetime lifetime, object? singletonInstance);

    /// <summary>
    /// Registers the given service.
    /// </summary>
    /// <param name="serviceType">The service to register.</param>
    /// <param name="implementationType">The implementation type of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <param name="lifetime">The lifetime of the service.</param>
    /// <param name="singletonInstance">The optional singleton instance.</param>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry Register(Type serviceType, Type implementationType, object? serviceKey, ServiceLifetime lifetime, object? singletonInstance, ServiceInstanceFactory? instanceFactory);

    /// <summary>
    /// Creates and registers a singleton service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T>(object? serviceKey = null);

    /// <summary>
    /// Creates and registers a singleton service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null);

    /// <summary>
    /// Registers a singleton service of the type <typeparamref name="T"/> using the given <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The instance that will be returned when requesting the <typeparamref name="T"/> service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T>(T instance, object? serviceKey = null);

    /// <summary>
    /// Creates and registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T, TImpl>(object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates and registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Registers a singleton service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/> using the given <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The instance that will be returned when requesting the <typeparamref name="T"/> service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterSingleton<T, TImpl>(TImpl instance, object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates and registers a transient service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterTransient<T>(object? serviceKey = null);

    /// <summary>
    /// Creates and registers a transient service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterTransient<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null);

    /// <summary>
    /// Creates and registers a transient service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterTransient<T, TImpl>(object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates and registers a transient service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterTransient<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates and registers a scoped service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterScoped<T>(object? serviceKey = null);

    /// <summary>
    /// Creates and registers a scoped service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterScoped<T>(ServiceInstanceFactory instanceFactory, object? serviceKey = null);

    /// <summary>
    /// Creates and registers a scoped service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterScoped<T, TImpl>(object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates and registers a scoped service of the type <typeparamref name="TImpl"/> for the service <typeparamref name="T"/>.
    /// </summary>
    ///  <param name="instanceFactory">The factory function that will be used to create the instance of the service.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The service to register.</typeparam>
    /// <typeparam name="TImpl">The implementation of the service.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry RegisterScoped<T, TImpl>(ServiceInstanceFactory<TImpl> instanceFactory, object? serviceKey = null) where TImpl : T;

    /// <summary>
    /// Creates a new scope which can be used to resolve scoped services.
    /// </summary>
    /// <returns>The instance of the scope.</returns>
    IScope CreateScope();

    /// <summary>
    /// Registers the given <paramref name="serviceDescriptor"/>.
    /// </summary>
    /// <param name="serviceDescriptor">The service descriptor.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <returns>THe current <see cref="IServiceRegistry"/> instance.</returns>
    IServiceRegistry Register(IServiceDescriptor serviceDescriptor, object? serviceKey);

    /// <summary>
    /// Unregisters a registered service of the type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>All disposable instances from <typeparamref name="T"/> will be disposed as usual, except if it is a service with the lifetime of a <see cref="ServiceLifetime.Singleton"/>.
    /// <para/>
    /// A singleton service will be disposed directly within the unregister method.
    /// </remarks>
    /// <param name="serviceKey">The optional service key.</param>
    /// <param name="successful">Determines whether the service was successfully unregistered.</param>
    /// <typeparam name="T">The service to unregister.</typeparam>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry UnregisterService<T>(object? serviceKey, out bool successful);

    /// <summary>
    /// Unregisters a registered service of the type <paramref name="serviceType"/>.
    /// </summary>
    /// <remarks>
    /// All disposable instances from <paramref name="serviceType"/> will be disposed as usual, except if it is a service with the lifetime of a <see cref="ServiceLifetime.Singleton"/>.
    /// <para/>
    /// A singleton service will be disposed directly within the unregister method.
    /// </remarks>
    /// <param name="serviceType">The service to unregister.</param>
    /// <param name="serviceKey">The optional service key.</param>
    /// <param name="successful">Determines whether the service was successfully unregistered.</param>
    /// <returns>The current <see cref="IServiceRegistry"/> for chaining calls.</returns>
    IServiceRegistry UnregisterService(Type serviceType, object? serviceKey, out bool successful);

    /// <summary>
    /// Checks whether the given <paramref name="serviceType"/> is registered.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <param name="serviceType">The type of the service.</param>
    /// <returns>Whether the given <paramref name="serviceType"/> is registered or not.</returns>
    bool IsServiceRegistered(Type serviceType, object? serviceKey);

    /// <summary>
    /// Checks whether the given <typeparamref name="T"/> is registered.
    /// </summary>
    /// <param name="serviceKey">The optional service key.</param>
    /// <typeparam name="T">The type of the service.</param>
    /// <returns>Whether the given <typeparamref name="T"/> is registered or not.</returns>
    bool IsServiceRegistered<T>(object? serviceKey);

}
