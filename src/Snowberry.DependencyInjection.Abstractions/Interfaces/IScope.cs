using System.Diagnostics.CodeAnalysis;

namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Defines a scope for services.
/// </summary>
public interface IScope :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    /// <summary>
    /// Gets called when the scope gets disposed.
    /// </summary>
    event EventHandler? OnDispose;

    /// <summary>
    /// Tries to get the cached instance for the given <paramref name="serviceIdentifier"/> within this scope.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service to look up.</param>
    /// <param name="instance">When this method returns, contains the cached instance if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a cached instance was found; otherwise <see langword="false"/>.</returns>
    bool TryGetScopedInstance(IServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance);

    /// <summary>
    /// Caches the given <paramref name="instance"/> for the specified <paramref name="serviceIdentifier"/> within this scope.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier of the service the instance belongs to.</param>
    /// <param name="instance">The instance to cache.</param>
    void AddCached(IServiceIdentifier serviceIdentifier, object instance);

    /// <summary>
    /// Gets or initializes the service provider to resolve services.
    /// </summary>
    IKeyedServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the disposable container for this scope.
    /// </summary>
    IDisposableContainer DisposableContainer { get; }

    /// <summary>
    /// Specifies whether this scope is the global scope.
    /// </summary>
    bool IsGlobalScope { get; }

    /// <summary>
    /// Returns whether the <see cref="IScope"/> has been disposed or not.
    /// </summary>
    bool IsDisposed { get; }
}
