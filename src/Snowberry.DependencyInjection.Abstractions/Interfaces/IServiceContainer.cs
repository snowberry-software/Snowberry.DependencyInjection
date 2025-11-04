namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Represents an inversion of control container.
/// </summary>
public interface IServiceContainer :
    IServiceRegistry,
    IServiceDescriptorReceiver,
    IServiceProvider,
    IKeyedServiceProvider,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    /// <summary>
    /// Returns whether the <see cref="IServiceContainer"/> has been disposed or not.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// The disposable container to register disposables into.
    /// </summary>
    IDisposableContainer DisposableContainer { get; }
}
