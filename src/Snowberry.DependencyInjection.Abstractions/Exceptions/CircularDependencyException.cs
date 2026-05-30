using System;
using System.Collections.Generic;
using System.Linq;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Gets thrown when a circular dependency is detected while building a service's resolver graph
/// (for example <c>A</c> depends on <c>B</c> and <c>B</c> depends on <c>A</c>). The cycle is detected
/// when the graph is built — on the first resolve of the offending service, or eagerly during
/// <see cref="Interfaces.IServiceContainer.Validate"/>.
/// </summary>
public sealed class CircularDependencyException : Exception
{
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
    /// The service whose resolution closed the cycle.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// The ordered dependency path leading into the cycle, ending with the service that closed it.
    /// </summary>
    public IReadOnlyList<Type> DependencyPath { get; }
}
