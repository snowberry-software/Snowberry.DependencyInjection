using System;
using System.Collections.Generic;
using System.Linq;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// The exception that is thrown when a circular dependency is detected between services
/// (for example, <c>A</c> depends on <c>B</c> while <c>B</c> depends on <c>A</c>).
/// </summary>
public sealed class CircularDependencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
    /// </summary>
    /// <param name="serviceType">The service whose resolution completed the dependency cycle.</param>
    /// <param name="dependencyPath">The ordered dependency path leading into the cycle, ending with the service that completed it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> or <paramref name="dependencyPath"/> is <see langword="null"/>.</exception>
    public CircularDependencyException(Type serviceType, IReadOnlyList<Type> dependencyPath)
        : base(FormatMessage(serviceType, dependencyPath))
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        DependencyPath = dependencyPath ?? throw new ArgumentNullException(nameof(dependencyPath));
    }

    private static string FormatMessage(Type serviceType, IReadOnlyList<Type> dependencyPath)
    {
        string path = dependencyPath is { Count: > 0 }
            ? string.Join(" -> ", dependencyPath.Select(t => t.FullName))
            : serviceType?.FullName ?? "<unknown>";

        return $"A circular dependency was detected while resolving '{serviceType?.FullName}'. Dependency path: {path}.";
    }

    /// <summary>
    /// Gets the service whose resolution completed the detected dependency cycle.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the ordered dependency path leading into the cycle, ending with the service that completed it.
    /// </summary>
    public IReadOnlyList<Type> DependencyPath { get; }
}
