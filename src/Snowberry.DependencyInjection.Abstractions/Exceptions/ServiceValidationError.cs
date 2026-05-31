using System;
using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Describes the kind of problem found while validating the registered service graph.
/// </summary>
public enum ServiceValidationErrorKind
{
    /// <summary>A required constructor parameter or <see cref="InjectAttribute"/> property dependency is not registered.</summary>
    MissingDependency,

    /// <summary>A circular dependency was detected.</summary>
    CircularDependency,

    /// <summary>The implementation type has no usable public constructor.</summary>
    NoPublicConstructor,
}

/// <summary>
/// Represents a single problem found when validating the registered service graph.
/// </summary>
public sealed class ServiceValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceValidationError"/> class.
    /// </summary>
    /// <param name="kind">The kind of problem that was found.</param>
    /// <param name="serviceType">The registered service the problem was found under.</param>
    /// <param name="message">A human-readable description of the problem.</param>
    /// <param name="dependencyType">The offending dependency type, or <see langword="null"/> when not applicable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
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
