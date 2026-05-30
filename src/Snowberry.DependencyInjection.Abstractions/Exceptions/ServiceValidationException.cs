using System;
using System.Collections.Generic;
using System.Linq;

namespace Snowberry.DependencyInjection.Abstractions.Exceptions;

/// <summary>
/// The exception that is thrown when the registered service graph has one or more problems
/// (missing required dependencies, circular dependencies, and similar). All problems are aggregated in
/// <see cref="Errors"/>.
/// </summary>
public sealed class ServiceValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceValidationException"/> class with the validation
    /// problems that were found.
    /// </summary>
    /// <param name="errors">The problems found during validation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
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

    /// <summary>Gets all problems found during validation.</summary>
    public IReadOnlyList<ServiceValidationError> Errors { get; }
}
