using System;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// The kind of problem found by <c>ServiceContainer.Validate</c> / <c>TryValidate</c>.
/// </summary>
public enum ServiceValidationErrorKind
{
    /// <summary>A required constructor parameter or <c>[Inject]</c> property dependency is not registered.</summary>
    MissingDependency,

    /// <summary>A circular dependency was detected.</summary>
    CircularDependency,

    /// <summary>The implementation type has no usable public constructor.</summary>
    NoPublicConstructor,
}

/// <summary>
/// A single problem found while eagerly validating the registered service graph. Collected (not thrown) by
/// <c>ServiceContainer.TryValidate</c> so every problem is reported at once.
/// </summary>
public sealed class ServiceValidationError
{
    public ServiceValidationError(ServiceValidationErrorKind kind, Type serviceType, string message, Type? dependencyType = null)
    {
        Kind = kind;
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DependencyType = dependencyType;
    }

    /// <summary>The kind of problem.</summary>
    public ServiceValidationErrorKind Kind { get; }

    /// <summary>The registered service the problem was found under.</summary>
    public Type ServiceType { get; }

    /// <summary>The offending dependency type, when applicable (missing dependency).</summary>
    public Type? DependencyType { get; }

    /// <summary>A human-readable description of the problem.</summary>
    public string Message { get; }

    /// <inheritdoc/>
    public override string ToString() => Message;
}
