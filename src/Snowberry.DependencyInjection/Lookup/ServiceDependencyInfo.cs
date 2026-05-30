namespace Snowberry.DependencyInjection.Lookup;

/// <summary>
/// A single construct-time dependency (constructor parameter or <c>[Inject]</c> property) of a service, used by
/// eager validation to walk the registered graph without constructing anything.
/// </summary>
internal readonly struct ServiceDependencyInfo
{
    public ServiceDependencyInfo(Type dependencyType, object? serviceKey, bool required, string? memberName, bool isProperty)
    {
        DependencyType = dependencyType;
        ServiceKey = serviceKey;
        Required = required;
        MemberName = memberName;
        IsProperty = isProperty;
    }

    public Type DependencyType { get; }

    public object? ServiceKey { get; }

    public bool Required { get; }

    public string? MemberName { get; }

    public bool IsProperty { get; }
}
