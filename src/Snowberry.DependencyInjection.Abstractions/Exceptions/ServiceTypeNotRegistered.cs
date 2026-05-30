namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when a requested service is not registered.
/// </summary>
public sealed class ServiceTypeNotRegistered : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTypeNotRegistered"/> class with a default message.
    /// </summary>
    /// <param name="serviceType">The requested service type that is not registered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    public ServiceTypeNotRegistered(Type serviceType) : base($"Service type '{serviceType.FullName}' is not registered!")
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTypeNotRegistered"/> class with the specified message.
    /// </summary>
    /// <param name="serviceType">The requested service type that is not registered.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    public ServiceTypeNotRegistered(Type serviceType, string message) : base(message)
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
    }

    /// <summary>
    /// The type of the requested service.
    /// </summary>
    public Type ServiceType { get; }
}
