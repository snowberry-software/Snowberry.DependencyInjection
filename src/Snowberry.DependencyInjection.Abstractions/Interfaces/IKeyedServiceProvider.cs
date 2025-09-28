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
    object? GetKeyedService(Type serviceType, object? serviceKey);
}
