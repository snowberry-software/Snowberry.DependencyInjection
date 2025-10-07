namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Provides a method to register disposable objects.
/// </summary>
public interface IDisposableContainer
#if NETCOREAPP
    : IAsyncDisposable, IDisposable
#else
    : IDisposable
#endif
{
    /// <summary>
    /// Registers a disposable object.
    /// </summary>
    /// <param name="disposable">The disposable object to register.</param>
    void RegisterDisposable(IDisposable disposable);

#if NETCOREAPP
    /// <summary>
    /// Registers a disposable object.
    /// </summary>
    /// <param name="disposable">The disposable object to register.</param>
    void RegisterDisposable(IAsyncDisposable disposable);
#endif

    /// <summary>
    /// Registers a disposable object.
    /// </summary>
    /// <param name="disposable">The disposable object to register.</param>
    void RegisterDisposable(object disposable);

    /// <summary>
    /// Gets the amount of registered disposable objects.
    /// </summary>
    int DisposableCount { get; }
}
