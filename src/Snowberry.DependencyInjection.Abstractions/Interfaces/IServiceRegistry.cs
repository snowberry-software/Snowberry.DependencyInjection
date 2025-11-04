namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Represents a registry to register services.
/// </summary>
public interface IServiceRegistry
{
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
