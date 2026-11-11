namespace Snowberry.DependencyInjection.Helper;

/// <summary>
/// Extension methods for instances and types.
/// </summary>
internal static class TypeHelper
{
    /// <summary>
    /// Determines whether <paramref name="instance"/> must be tracked for disposal.
    /// </summary>
    /// <param name="instance">The instance to test.</param>
    /// <returns><see langword="true"/> if the instance implements <see cref="IDisposable"/> or <c>IAsyncDisposable</c>; otherwise, <see langword="false"/>.</returns>
    public static bool IsDisposable(this object? instance)
    {
        if (instance == null)
            return false;

        if (instance is IDisposable)
            return true;

#if NETCOREAPP
        if (instance is IAsyncDisposable)
            return true;
#endif

        return false;
    }

    /// <summary>
    /// Determines whether instances whose runtime type is exactly <paramref name="type"/> must be tracked for disposal.
    /// </summary>
    /// <param name="type">The exact runtime type of the instances.</param>
    /// <returns><see langword="true"/> if the type implements <see cref="IDisposable"/> or <c>IAsyncDisposable</c>; otherwise, <see langword="false"/>.</returns>
    public static bool IsDisposableType(Type type)
    {
        if (typeof(IDisposable).IsAssignableFrom(type))
            return true;

#if NETCOREAPP
        if (typeof(IAsyncDisposable).IsAssignableFrom(type))
            return true;
#endif

        return false;
    }
}
