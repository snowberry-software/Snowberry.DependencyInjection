namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Defines a factory for creating new scope instances used to control the lifetime of service objects.
/// </summary>
public interface IServiceScopeFactory
{
    /// <summary>
    /// Creates a new <see cref="IScope"/> instance.
    /// </summary>
    /// <returns>The scope.</returns>
    IScope CreateScope();
}
