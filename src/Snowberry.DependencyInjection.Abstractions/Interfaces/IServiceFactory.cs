using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// The factory that will be used to retrieve service instances.
/// </summary>
public interface IServiceFactory
{
    /// <summary>
    /// Creates a new instance of the given <paramref name="type"/> and injects the services during initialization.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="genericTypeArguments">The generic type arguments.</param>
    /// <returns>The instance.</returns>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    object CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, IServiceProvider serviceProvider, Type[]? genericTypeArguments = null);

    /// <summary>
    /// Creates a new instance of the given <paramref name="type"/> and injects the services during initialization.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="genericTypeArguments">The generic type arguments.</param>
    /// <returns>The instance.</returns>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(IServiceProvider serviceProvider, Type[]? genericTypeArguments = null);

    /// <summary>
    /// Gets the constructor for the specified type.
    /// </summary>
    /// <param name="instanceType">The type to get the constructor for.</param>
    /// <returns>The constructor information.</returns>
    ConstructorInfo? GetConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type instanceType);
}