namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Defines a scope for services.
/// </summary>
public interface IScope : IDisposableContainer, IDisposable
{
    /// <summary>
    /// Gets called when the scope gets disposed.
    /// </summary>
    event EventHandler? OnDispose;

    /// <summary>
    /// Sets the current <see cref="IServiceFactory"/>.
    /// </summary>
    /// <param name="serviceFactory">The service factory to use.</param>
    void SetServiceFactory(IServiceFactory serviceFactory);

    /// <summary>
    /// Gets or initializes the service factory to resolve services.
    /// </summary>
    IServiceFactory ServiceFactory { get; }

    /// <summary>
    /// Returns whether the <see cref="IScope"/> has been disposed or not.
    /// </summary>
    bool IsDisposed { get; }
}
