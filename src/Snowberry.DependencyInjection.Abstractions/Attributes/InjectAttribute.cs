namespace Snowberry.DependencyInjection.Abstractions.Attributes;

/// <summary>
/// Indicates that the associated property should have a value injected during initialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class InjectAttribute : Attribute
{
    /// <summary>
    /// Creates a new indicator for the associated property that a required service must be injected during initialization.
    /// </summary>
    public InjectAttribute() : this(true)
    {
    }

    /// <summary>
    /// Creates a new indicator for the associated property with the option to determine whether the service must be injected during initialization.
    /// </summary>
    /// <param name="isRequired">Determines whether the service is optional or required.</param>
    public InjectAttribute(bool isRequired)
    {
        IsRequired = isRequired;
    }

    /// <summary>
    /// Determines whether the service is optional or required.
    /// </summary>
    public bool IsRequired { get; }
}
