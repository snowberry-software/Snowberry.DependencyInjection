using System.Diagnostics.CodeAnalysis;

namespace Snowberry.DependencyInjection.Helper;

/// <summary>
/// Helper methods for throwing exceptions.
/// </summary>
public static class DisposeThrowHelper
{
    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the given <paramref name="condition"/> is true.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="instance">The instance.</param>
    /// <exception cref="ObjectDisposedException">The exception.</exception>
    public static void ThrowIfDisposed([DoesNotReturnIf(true)] bool condition, object? instance)
    {
        if (!condition)
            return;

        throw new ObjectDisposedException(instance?.GetType().FullName);
    }
}
