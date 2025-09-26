using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IServiceRegistry"/>.
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
}
