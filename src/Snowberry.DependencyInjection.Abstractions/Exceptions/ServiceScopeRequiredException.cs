namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when a requested service requires a service scope.
/// </summary>
public sealed class ServiceScopeRequiredException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceScopeRequiredException"/> class.
    /// </summary>
    /// <param name="serviceType">The requested service type that requires a service scope.</param>
    public ServiceScopeRequiredException(Type serviceType)
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// The type of the requested service.
    /// </summary>
    public Type ServiceType { get; }
}