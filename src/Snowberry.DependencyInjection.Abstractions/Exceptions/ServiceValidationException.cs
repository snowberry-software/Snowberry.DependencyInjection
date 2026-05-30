using System;
using System.Collections.Generic;
using System.Linq;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// Thrown by <c>ServiceContainer.Validate</c> when the registered service graph has one or more problems
/// (missing required dependencies, circular dependencies, …). All problems are aggregated in
/// <see cref="Errors"/>; use <c>TryValidate</c> for the non-throwing variant.
/// </summary>
public sealed class ServiceValidationException : Exception
{
    public ServiceValidationException(IReadOnlyList<ServiceValidationError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    private static string FormatMessage(IReadOnlyList<ServiceValidationError> errors)
    {
        if (errors is not { Count: > 0 })
            return "Service validation failed.";

        return $"Service validation failed with {errors.Count} error(s):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, errors.Select(e => " - " + e.Message));
    }

    /// <summary>All problems found during validation.</summary>
    public IReadOnlyList<ServiceValidationError> Errors { get; }
}
