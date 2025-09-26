namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Retrieves services using a key and a type.
/// </summary>
public interface IKeyedServiceProvider : IServiceProvider
{
    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of the requested service.</param>
    /// <param name="serviceKey">The key of the requested service.</param>
    /// <returns>The requested service instance.</returns>
    object GetKeyedService(Type serviceType, object? serviceKey);

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <remarks>The method will not throw an exception if the service hasn't been found.</remarks>
    /// <param name="serviceType">The type of the requested service.</param>
    /// <param name="serviceKey">The key of the requested service.</param>
    /// <returns>The requested service instance.</returns>
    object? GetOptionalKeyedService(Type serviceType, object? serviceKey);

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="serviceKey">The key of the requested service.</param>
    /// <returns>The requested service instance.</returns>
    T GetKeyedService<T>(object? serviceKey);

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <remarks>The method will not throw an exception if the service hasn't been found.</remarks>
    /// <param name="serviceKey">The key of the requested service.</param>
    /// <returns>The requested service instance.</returns>
    T? GetOptionalKeyedService<T>(object? serviceKey);
}
