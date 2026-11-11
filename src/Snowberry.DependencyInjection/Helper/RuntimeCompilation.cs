using System.Runtime.CompilerServices;

namespace Snowberry.DependencyInjection.Helper;

/// <summary>
/// Decides when activation delegates are compiled with <c>System.Linq.Expressions</c>.
/// </summary>
/// <remarks>
/// Activation is tiered: the first <see cref="CompileAfterInvocations"/> - 1 invocations run a reflection-based
/// (interpreted) path, and the next invocation compiles an expression tree that serves every later call. Services
/// that are only activated once, such as singletons, are never compiled. When <see cref="IsSupported"/> is
/// <see langword="false"/> (Native AOT, interpreter-only platforms) compilation is never attempted and the
/// reflection path serves every call.
/// </remarks>
internal static class RuntimeCompilation
{
    // Compiling is skipped without a JIT because Expression.Compile would fall back to the expression interpreter,
    // which is much slower than the reflection path.

    /// <summary>
    /// The invocation on which an activation delegate is compiled. Earlier invocations use reflection.
    /// </summary>
    internal const int CompileAfterInvocations = 2;

    /// <summary>
    /// Gets a value indicating whether expression trees are compiled to IL on the current runtime.
    /// </summary>
    internal static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if NETCOREAPP
            return RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
#else
            return true;
#endif
        }
    }
}
