using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Lookup.Cache;

public readonly struct ScopeServiceCacheEntry : IScopeServiceCacheEntry
{
    public ScopeServiceCacheEntry(IScope? scope, IServiceIdentifier serviceIdentifier, object instance)
    {
        Scope = scope;
        ServiceIdentifier = serviceIdentifier ?? throw new ArgumentNullException(nameof(serviceIdentifier));
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    /// <inheritdoc/>
    public IScope? Scope { get; }

    /// <inheritdoc/>
    public IServiceIdentifier ServiceIdentifier { get; }

    /// <inheritdoc/>
    public object Instance { get; }
}
