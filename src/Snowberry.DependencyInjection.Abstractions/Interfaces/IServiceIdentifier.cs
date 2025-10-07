namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Used to identify a service.
/// </summary>
public interface IServiceIdentifier : IEquatable<IServiceIdentifier>
{
    /// <summary>
    /// The type of the service.
    /// </summary>
    Type ServiceType { get; }

    /// <summary>
    /// The optional service key.
    /// </summary>
    object? ServiceKey { get; }
}
