namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// The factory that will be used to retrieve service instances.
/// </summary>
public interface IServiceFactory : IServiceProvider, IKeyedServiceProvider
{
    /// <summary>
    /// Creates a new instance of the given <paramref name="type"/> and injects the services during initialization.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <param name="genericTypeParameters">The generic type parameters if required.</param>
    /// <returns>The instantiated instance.</returns>
    object CreateInstance(Type type, Type[]? genericTypeParameters = null);

    /// <summary>
    /// Creates a new instance of the given <typeparamref name="T"/> and injects the services during initialization.
    /// </summary>
    /// <typeparam name="T">The type to instantiate.</param>
    /// <param name="genericTypeParameters">The generic type parameters if required.</param>
    /// <returns>The instantiated instance as <typeparamref name="T"/>.</returns>
    T CreateInstance<T>(Type[]? genericTypeParameters = null);
}
