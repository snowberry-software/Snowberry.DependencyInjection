namespace Snowberry.DependencyInjection.Abstractions.Interfaces;

/// <summary>
/// Represents an inversion of control container.
/// </summary>
public interface IServiceContainer : 
    IServiceRegistry, 
    IServiceFactory, 
    IServiceDescriptorReceiver, 
    IDisposableContainer, 
    IDisposable
{
    /// <summary>
    /// Returns whether the <see cref="IServiceContainer"/> has been disposed or not.
    /// </summary>
    bool IsDisposed { get; }
}
