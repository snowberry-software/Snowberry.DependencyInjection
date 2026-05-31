using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when the <see cref="IServiceFactory"/> could not instantiate a new instance for the service type.
/// </summary>
public sealed class InvalidServiceImplementationType : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidServiceImplementationType"/> class with a default message.
    /// </summary>
    /// <param name="serviceImplementationType">The implementation type that could not be instantiated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceImplementationType"/> is <see langword="null"/>.</exception>
    public InvalidServiceImplementationType(Type serviceImplementationType) : base($"Cannot instantiate abstract classes or interfaces! ({serviceImplementationType.FullName})")
    {
        ServiceImplementationType = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidServiceImplementationType"/> class with the specified message.
    /// </summary>
    /// <param name="serviceImplementationType">The implementation type that could not be instantiated.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceImplementationType"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public InvalidServiceImplementationType(Type serviceImplementationType, string message) : base(message)
    {
        ServiceImplementationType = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));
        _ = message ?? throw new ArgumentNullException(nameof(message));
    }

    /// <summary>
    /// The implementation type of the service.
    /// </summary>
    public Type ServiceImplementationType { get; }
}
