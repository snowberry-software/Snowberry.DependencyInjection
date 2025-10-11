using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection;

/// <summary>
/// <see cref="IServiceFactory"/> for a per-scope use.
/// </summary>
public sealed class ScopeServiceFactory : IServiceFactory
{
    /// <summary>
    /// Creates a new service factory for the given <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="serviceFactory">The service factory that will be used to resolve all scope related requests.</param>
    public ScopeServiceFactory(IScope scope, IServiceFactoryScoped serviceFactory)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ServiceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
    }

    /// <inheritdoc/>
    public object CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, Type[]? genericTypeParameters = null)
    {
        return ServiceFactory.CreateInstance(type, Scope, genericTypeParameters);
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    public T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(Type[]? genericTypeParameters = null)
    {
        return ServiceFactory.CreateInstance<T>(Scope, genericTypeParameters);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        return ServiceFactory.GetService(serviceType, Scope);
    }

    /// <inheritdoc/>
    public ConstructorInfo? GetConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type instanceType)
    {
        return ServiceFactory.GetConstructor(instanceType);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        return ServiceFactory.GetKeyedService(serviceType, serviceKey, Scope);
    }

    /// <summary>
    /// The service factory that will be used to resolve all scope related requests.
    /// </summary>
    public IServiceFactoryScoped ServiceFactory { get; }

    /// <summary>
    /// The scope that owns this <see cref="IServiceFactory"/>.
    /// </summary>
    public IScope Scope { get; }
}
