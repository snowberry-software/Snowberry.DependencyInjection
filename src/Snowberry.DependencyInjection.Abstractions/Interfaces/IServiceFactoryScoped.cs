using System.Reflection;

namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// <see cref="IServiceFactory"/> that is used for scopes.
/// </summary>
/// <remarks>The root scope will always be <see langword="null"/>.</remarks>
public interface IServiceFactoryScoped
{
    /// <summary>
    /// Notifies the <see cref="IServiceFactoryScoped"/> that a new scope has been created.
    /// </summary>
    /// <param name="scope">The scope that has been created.</param>
    void NotifyScopeCreated(IScope scope);

    /// <summary>
    /// Notifies the <see cref="IServiceFactoryScoped"/> that a new scope has been disposed.
    /// </summary>
    /// <param name="scope">The scope that has been disposed.</param>
    void NotifyScopeDisposed(IScope? scope);

    /// <inheritdoc cref="IServiceProvider.GetService(Type)"/>
    object GetService(Type serviceType, IScope scope);

    /// <inheritdoc cref="IServiceFactory.GetService{T}"/>
    T GetService<T>(IScope scope);

    /// <inheritdoc cref="IServiceFactory.GetOptionalService(Type)"/>
    object? GetOptionalService(Type serviceType, IScope scope);

    /// <inheritdoc cref="IServiceFactory.GetOptionalService{T}()"/>
    T? GetOptionalService<T>(IScope scope);

    /// <inheritdoc cref="IServiceFactory.CreateInstance(Type, Type[]?)"/>
    object CreateInstance(Type type, IScope scope, Type[]? genericTypeParameters = null);

    /// <inheritdoc cref="IServiceFactory.CreateInstance{T}(Type[]?)"/>
    T CreateInstance<T>(IScope scope, Type[]? genericTypeParameters = null);

    /// <inheritdoc cref="IServiceFactory.GetConstructor(Type)"/>
    ConstructorInfo? GetConstructor(Type instanceType);

    /// <inheritdoc cref="IKeyedServiceProvider.GetKeyedService(Type, object?)"/>
    object GetKeyedService(Type serviceType, object? serviceKey, IScope scope);

    /// <inheritdoc cref="IKeyedServiceProvider.GetOptionalKeyedService(Type, object?)"/>
    object? GetOptionalKeyedService(Type serviceType, object? serviceKey, IScope scope);

    /// <inheritdoc cref="IKeyedServiceProvider.GetKeyedService{T}(object?)"/>
    T GetKeyedService<T>(object? serviceKey, IScope scope);

    /// <inheritdoc cref="IKeyedServiceProvider.GetOptionalKeyedService(Type, object?)"/>
    T? GetOptionalKeyedService<T>(object? serviceKey, IScope scope);
}
