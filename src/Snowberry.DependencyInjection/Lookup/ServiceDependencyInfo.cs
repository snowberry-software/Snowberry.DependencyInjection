namespace Snowberry.DependencyInjection.Lookup;

/// <summary>
/// Represents a single dependency of a service, either a constructor parameter or a property marked with
/// <see cref="Snowberry.DependencyInjection.Abstractions.Attributes.InjectAttribute"/>.
/// </summary>
internal readonly struct ServiceDependencyInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceDependencyInfo"/> struct.
    /// </summary>
    /// <param name="dependencyType">The type of the dependency that must be resolved.</param>
    /// <param name="serviceKey">The optional service key used to resolve the dependency, or <see langword="null"/> for the default registration.</param>
    /// <param name="required">Whether the dependency must be resolvable.</param>
    /// <param name="memberName">The name of the constructor parameter or property the dependency belongs to, or <see langword="null"/> if unspecified.</param>
    /// <param name="isProperty"><see langword="true"/> if the dependency is an injected property; <see langword="false"/> if it is a constructor parameter.</param>
    public ServiceDependencyInfo(Type dependencyType, object? serviceKey, bool required, string? memberName, bool isProperty)
    {
        DependencyType = dependencyType;
        ServiceKey = serviceKey;
        Required = required;
        MemberName = memberName;
        IsProperty = isProperty;
    }

    /// <summary>
    /// Gets the type of the dependency that must be resolved.
    /// </summary>
    public Type DependencyType { get; }

    /// <summary>
    /// Gets the optional service key used to resolve the dependency, or <see langword="null"/> for the default registration.
    /// </summary>
    public object? ServiceKey { get; }

    /// <summary>
    /// Gets a value indicating whether the dependency must be resolvable.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the name of the constructor parameter or property the dependency belongs to, or <see langword="null"/> if unspecified.
    /// </summary>
    public string? MemberName { get; }

    /// <summary>
    /// Gets a value indicating whether the dependency is an injected property; otherwise it is a constructor parameter.
    /// </summary>
    public bool IsProperty { get; }
}
