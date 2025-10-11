using System.Diagnostics.CodeAnalysis;
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
    object? GetService(Type serviceType, IScope scope);

    /// <inheritdoc cref="IServiceFactory.CreateInstance(Type, Type[]?)"/>
    object CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, IScope scope, Type[]? genericTypeParameters = null);

    /// <inheritdoc cref="IServiceFactory.CreateInstance{T}(Type[]?)"/>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(IScope scope, Type[]? genericTypeParameters = null);

    /// <summary>
    /// Gets the constructor for the specified type.
    /// </summary>
    /// <param name="instanceType">The type to get the constructor for.</param>
    /// <returns>The constructor information.</returns>
    ConstructorInfo? GetConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type instanceType);

    /// <inheritdoc cref="IKeyedServiceProvider.GetKeyedService(Type, object?)"/>
    object? GetKeyedService(Type serviceType, object? serviceKey, IScope scope);
}
