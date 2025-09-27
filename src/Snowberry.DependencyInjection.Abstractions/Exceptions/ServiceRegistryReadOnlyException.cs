namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when attempting to modify a read-only service registry.
/// </summary>
/// <param name="message">The message.</param>
public sealed class ServiceRegistryReadOnlyException(string message) : Exception(message);
