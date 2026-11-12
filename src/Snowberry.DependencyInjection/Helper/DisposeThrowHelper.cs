using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Snowberry.DependencyInjection.Helper;

/// <summary>
/// Throw helpers for objects that have been disposed.
/// </summary>
public static class DisposeThrowHelper
{
    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition"><see langword="true"/> if <paramref name="instance"/> has been disposed.</param>
    /// <param name="instance">The object being checked; its type name is reported as the disposed object's name.</param>
    /// <exception cref="ObjectDisposedException"><paramref name="condition"/> is <see langword="true"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed([DoesNotReturnIf(true)] bool condition, object? instance)
    {
        // The throw lives in a separate method so this check stays small enough to inline into every resolve.
        if (condition)
            ThrowObjectDisposed(instance);
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> naming the type of <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The disposed instance.</param>
    /// <exception cref="ObjectDisposedException">Always.</exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposed(object? instance)
    {
        throw new ObjectDisposedException(instance?.GetType().FullName);
    }
}
