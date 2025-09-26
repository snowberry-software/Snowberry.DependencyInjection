namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Used for storing scope specific service instances.
/// </summary>
public interface IScopeServiceCacheEntry
{
    /// <summary>
    /// The scope for the entry.
    /// </summary>
    IScope? Scope { get; }

    /// <summary>
    /// The service identifier.
    /// </summary>
    IServiceIdentifier ServiceIdentifier { get; }

    /// <summary>
    /// The instance of the service type.
    /// </summary>
    object Instance { get; }
}
