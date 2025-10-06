using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Snowberry.DependencyInjection.Abstractions.Helper;

public static class ThrowHelper
{
    /// <summary>
    /// Throws a <see cref="InvalidCastException"/> for the given <paramref name="serviceImplementationType"/>.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <param name="serviceImplementationType">The implementation type of the service.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowInvalidServiceImplementationCast(Type serviceType, Type serviceImplementationType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        _ = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));

        throw new InvalidCastException($"Service implementation '{serviceType.GetType().FullName}' can't be cast to '{serviceImplementationType.FullName}'!");
    }

    /// <summary>
    /// Throws a <see cref="InvalidOperationException"/> for the given <paramref name="serviceImplementationType"/>.
    /// </summary>
    /// <param name="serviceImplementationType">The service that has no valid constructors.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowInvalidConstructor(Type serviceImplementationType)
    {
        _ = serviceImplementationType ?? throw new ArgumentNullException(nameof(serviceImplementationType));

        throw new InvalidOperationException($"The type `{serviceImplementationType.FullName}` has no valid constructors and could not be created!");
    }

    /// <summary>
    /// Throws a <see cref="NotImplementedException"/> for the given <paramref name="lifetime"/>.
    /// </summary>
    /// <param name="lifetime">The lifetime that isn't implemented.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object ThrowServiceLifetimeNotImplemented(ServiceLifetime lifetime)
    {
        throw new NotImplementedException($"The provided service lifetime is not supported {lifetime}!");
    }
}
