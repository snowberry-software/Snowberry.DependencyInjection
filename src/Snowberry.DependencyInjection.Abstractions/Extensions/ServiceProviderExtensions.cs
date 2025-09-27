using Snowberry.DependencyInjection.Abstractions.Exceptions;

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
    /// <param name="serviceType">The service type.</param>
    /// <returns>The required service, otherwise an exception is thrown.</returns>
    public static object GetRequiredService(this IServiceProvider serviceProvider, Type serviceType)
    {
        _ = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return serviceProvider.GetService(serviceType) is object result ? result : throw new ServiceTypeNotRegistered(serviceType);
    }
}
