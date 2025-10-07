namespace Snowberry.DependencyInjection.Abstractions.Attributes;

/// <summary>
/// Specifies which key should be used to receive the keyed service.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromKeyedServicesAttribute : Attribute
{
    public FromKeyedServicesAttribute(object? serviceKey)
    {
        ServiceKey = serviceKey;
    }

    /// <summary>
    /// The optional service key.
    /// </summary>
    public object? ServiceKey { get; }
}
