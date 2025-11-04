namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when a requested service requires a service scope.
/// </summary>
public sealed class ServiceScopeRequiredException : Exception
{
    public ServiceScopeRequiredException(Type serviceType)
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// The type of the requested service.
    /// </summary>
    public Type ServiceType { get; }
}