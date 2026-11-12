using System.Runtime.CompilerServices;

namespace Snowberry.DependencyInjection.Abstractions.Helper;

/// <summary>
/// Provides helper methods for throwing common service-related exceptions.
/// </summary>
public static class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="InvalidCastException"/> reporting that an instance of
    /// <paramref name="serviceImplementationType"/> cannot be used as <paramref name="serviceType"/>.
    /// </summary>
    /// <param name="serviceType">The requested service type.</param>
    /// <param name="serviceImplementationType">The runtime type of the instance that was produced.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> or <paramref name="serviceImplementationType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidCastException">Always, when both arguments are set.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowInvalidServiceImplementationCast(Type serviceType, Type serviceImplementationType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));

        throw new InvalidCastException($"Service implementation '{serviceImplementationType.FullName}' can't be cast to '{serviceType.FullName}'!");
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> reporting that <paramref name="serviceImplementationType"/>
    /// has no usable public constructor.
    /// </summary>
    /// <param name="serviceImplementationType">The implementation type that has no usable public constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceImplementationType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Always, when <paramref name="serviceImplementationType"/> is set.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowInvalidConstructor(Type serviceImplementationType)
    {
        _ = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));

        throw new InvalidOperationException($"The type `{serviceImplementationType.FullName}` has no valid constructors and could not be created!");
    }

    /// <summary>
    /// Throws a <see cref="NotImplementedException"/> reporting that <paramref name="lifetime"/> is not supported.
    /// </summary>
    /// <param name="lifetime">The lifetime that is not supported.</param>
    /// <returns>Never returns; the return type lets callers use the call as an expression.</returns>
    /// <exception cref="NotImplementedException">Always.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object ThrowServiceLifetimeNotImplemented(ServiceLifetime lifetime)
    {
        throw new NotImplementedException($"The provided service lifetime is not supported {lifetime}!");
    }
}
