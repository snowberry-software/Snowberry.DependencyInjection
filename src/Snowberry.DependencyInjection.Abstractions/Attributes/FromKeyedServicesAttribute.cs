namespace Snowberry.DependencyInjection.Abstractions.Attributes;

/// <summary>
/// Specifies which key should be used to receive the keyed service.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromKeyedServicesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FromKeyedServicesAttribute"/> class.
    /// </summary>
    /// <param name="serviceKey">The key used to resolve the keyed service, or <see langword="null"/> for the default registration.</param>
    public FromKeyedServicesAttribute(object? serviceKey)
    {
        ServiceKey = serviceKey;
    }

    /// <summary>
    /// The optional service key.
    /// </summary>
    public object? ServiceKey { get; }
}
