namespace Snowberry.DependencyInjection;

[Flags]
public enum ServiceContainerOptions : byte
{
    /// <summary>
    /// No additional options.
    /// </summary>
    None = 0,

    /// <summary>
    /// Marks all registered services as read-only so the lifetime or implementation type can't be overwritten by re-registering the service type with new options.
    /// </summary>
    ReadOnly = 1 << 0,

    /// <summary>
    /// The default options.
    /// </summary>
    Default = ReadOnly
}
