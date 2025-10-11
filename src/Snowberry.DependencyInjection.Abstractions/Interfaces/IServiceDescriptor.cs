using System.Diagnostics.CodeAnalysis;

namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Represents a factory function that creates an instance of a service.
/// </summary>
/// <param name="serviceProvider">The service provider to use for resolving dependencies.</param>
/// <param name="serviceKey">An optional key to identify the service instance, if applicable.</param>
/// <returns>An instance of the service implementation.</returns>
public delegate object ServiceInstanceFactory(IServiceProvider serviceProvider, object? serviceKey);

/// <summary>
/// Represents a factory function that creates an instance of a service.
/// </summary>
/// <typeparam name="TImpl"> The type of the service implementation.</typeparam>
/// <param name="serviceProvider">The service provider to use for resolving dependencies.</param>
/// <param name="serviceKey">An optional key to identify the service instance, if applicable.</param>
/// <returns>An instance of the service implementation.</returns>
public delegate TImpl ServiceInstanceFactory<TImpl>(IServiceProvider serviceProvider, object? serviceKey);

/// <summary>
/// Represents the data of a registered service.
/// </summary>
public interface IServiceDescriptor
{
    /// <summary>
    /// Clones the <see cref="IServiceDescriptor"/> for the specified <paramref name="serviceType"/>.
    /// </summary>
    /// <remarks>The <see cref="SingletonInstance"/> will be ignored.</remarks>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The cloned <see cref="IServiceDescriptor"/>.</returns>
    IServiceDescriptor CloneFor(Type serviceType);

    /// <summary>
    /// The instance of the <see cref="ImplementationType"/>.
    /// </summary>
    object? SingletonInstance { get; set; }

    /// <summary>
    /// The factory function to create an instance of the service.
    /// </summary>
    ServiceInstanceFactory? InstanceFactory { get; set; }

    /// <summary>
    /// The registered service type.
    /// </summary>
    Type ServiceType { get; }

    /// <summary>
    /// The type that represents the implementation of the service.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)]
    Type ImplementationType { get; }

    /// <summary>
    /// The lifetime of the service.
    /// </summary>
    ServiceLifetime Lifetime { get; }
}
