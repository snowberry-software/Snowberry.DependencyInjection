using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;

namespace Snowberry.DependencyInjection.Lookup;

/// <see cref="IServiceFactory"/>.
public class DefaultServiceScopeProvider : IScope, IServiceProvider, IKeyedServiceProvider
{
    /// <inheritdoc/>
    public event EventHandler? OnDispose;

    private readonly ServiceContainer _rootProvider;
    private readonly bool _isRootScope;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private bool _isDisposed;
    private DisposableContainer _disposableContainer = new();

    private Dictionary<IServiceIdentifier, object> _scopedInstances = [];

    public DefaultServiceScopeProvider(ServiceContainer rootProvider, bool isRootScope)
    {
        _rootProvider = rootProvider;
        _isRootScope = isRootScope;
    }

    /// <inheritdoc/>
    public bool TryGetScopedInstance(IServiceIdentifier serviceIdentifier, [NotNullWhen(true)] out object? instance)
    {
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            return _scopedInstances.TryGetValue(serviceIdentifier, out instance);
        }
    }

    /// <inheritdoc/>
    public void AddCached(IServiceIdentifier serviceIdentifier, object instance)
    {
        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            _scopedInstances[serviceIdentifier] = instance;
        }
    }

    /// <summary>
    /// Disposes the core.
    /// </summary>
    /// <returns>Whether the core is already disposed.</returns>
    private bool DisposeCore()
    {
        if (_isDisposed)
            return true;

        lock (_lock)
        {
            if (_isDisposed)
                return true;

            _isDisposed = true;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            _disposableContainer.Dispose();
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }

#if NETCOREAPP
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (DisposeCore())
            return;

        try
        {
            await _disposableContainer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            OnDispose?.Invoke(this, EventArgs.Empty);
        }
    }
#endif

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        return GetKeyedService(serviceType, serviceKey: null);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        lock (_lock)
        {
            DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);
            return _rootProvider.GetKeyedService(serviceType, this, serviceKey);
        }
    }

    /// <inheritdoc/>
    public bool IsDisposed
    {
        get
        {
            if (_isDisposed)
                return true;

            lock (_lock)
            {
                return _isDisposed;
            }
        }
    }

    /// <inheritdoc/>
    public IDisposableContainer DisposableContainer => _disposableContainer;

    /// <inheritdoc/>
    public IKeyedServiceProvider ServiceProvider => this;

    /// <inheritdoc/>
    public bool IsGlobalScope => _isRootScope;
}
