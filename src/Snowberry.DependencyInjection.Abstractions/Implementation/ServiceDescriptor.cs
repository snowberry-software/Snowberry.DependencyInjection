using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Implementation;

/// <inheritdoc cref="IServiceDescriptor"/>
public class ServiceDescriptor : IServiceDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDescriptor"/>.
    /// </summary>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type that implements the service.</param>
    /// <param name="lifetime">The lifetime of the service (Singleton, Scoped, or Transient).</param>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="serviceType"/> or <paramref name="implementationType"/> is null.</exception>
    public ServiceDescriptor(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType,
        ServiceLifetime lifetime)
        : this(serviceType, implementationType, lifetime, singletonInstance: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDescriptor"/>.
    /// </summary>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type that implements the service.</param>
    /// <param name="lifetime">The lifetime of the service (Singleton, Scoped, or Transient).</param>
    /// <param name="singletonInstance">An optional instance to use for the service when the <paramref name="lifetime"/> is <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="serviceType"/> or <paramref name="implementationType"/> is null.</exception>
    public ServiceDescriptor(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType,
        ServiceLifetime lifetime,
        object? singletonInstance)
    {
        if (singletonInstance != null && lifetime != ServiceLifetime.Singleton)
            throw new ArgumentException("Singleton instance can only be provided for singleton services.");

        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
        Lifetime = lifetime;
        SingletonInstance = singletonInstance;
    }

    /// <summary>
    /// Creates a <see cref="ServiceDescriptor"/> for a singleton service.
    /// </summary>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type that implements the service.</param>
    /// <param name="singletonInstance">The instance to use as the singleton for the service.</param>
    /// <returns>A <see cref="ServiceDescriptor"/> representing a singleton service registration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="serviceType"/> or <paramref name="implementationType"/> is null.</exception>
    public static ServiceDescriptor Singleton(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType,
        object? singletonInstance)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = implementationType ?? throw new ArgumentNullException(nameof(implementationType));

        return new(serviceType, implementationType, ServiceLifetime.Singleton, singletonInstance);
    }

    /// <summary>
    /// Creates a <see cref="ServiceDescriptor"/> for a transient service.
    /// </summary>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type that implements the service.</param>
    /// <returns>A <see cref="ServiceDescriptor"/> representing a transient service registration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="serviceType"/> or <paramref name="implementationType"/> is null.</exception>
    public static ServiceDescriptor Transient(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = implementationType ?? throw new ArgumentNullException(nameof(implementationType));

        return new(serviceType, implementationType, ServiceLifetime.Transient, singletonInstance: null);
    }

    /// <summary>
    /// Creates a <see cref="ServiceDescriptor"/> for a scoped service.
    /// </summary>
    /// <param name="serviceType">The type of the service to register.</param>
    /// <param name="implementationType">The type that implements the service.</param>
    /// <returns>A <see cref="ServiceDescriptor"/> representing a scoped service registration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either <paramref name="serviceType"/> or <paramref name="implementationType"/> is null.</exception>
    public static ServiceDescriptor Scoped(
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type implementationType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = implementationType ?? throw new ArgumentNullException(nameof(implementationType));

        return new(serviceType, implementationType, ServiceLifetime.Scoped, singletonInstance: null);
    }

    /// <inheritdoc/>
    public IServiceDescriptor CloneFor(Type serviceType)
    {
        return new ServiceDescriptor(serviceType, ImplementationType, Lifetime, SingletonInstance)
        {
            InstanceFactory = InstanceFactory
        };
    }

    /// <inheritdoc/>
    public object? SingletonInstance { get; set; }

    /// <inheritdoc/>
    public Type ServiceType { get; }

    /// <inheritdoc/>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type ImplementationType { get; }

    /// <inheritdoc/>
    public ServiceLifetime Lifetime { get; }

    /// <inheritdoc/>
    public ServiceInstanceFactory? InstanceFactory { get; set; }
}
