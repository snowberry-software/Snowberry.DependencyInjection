namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Implements both service factories (<see cref="IServiceFactory"/>, <see cref="IServiceFactoryScoped"/>).
/// </summary>
public interface IScopedServiceFactory : IServiceFactory, IServiceFactoryScoped
{
}
