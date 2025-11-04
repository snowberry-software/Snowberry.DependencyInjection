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

    bool TryGetScopedInstance(IServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance);

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
