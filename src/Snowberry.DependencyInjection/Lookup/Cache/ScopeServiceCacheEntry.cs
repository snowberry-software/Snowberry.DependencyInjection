using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Lookup.Cache;

/// <inheritdoc cref="IScopeServiceCacheEntry"/>
public readonly struct ScopeServiceCacheEntry : IScopeServiceCacheEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeServiceCacheEntry"/> struct.
    /// </summary>
    /// <param name="scope">The scope the cached instance belongs to, or <see langword="null"/> for the global scope.</param>
    /// <param name="serviceIdentifier">The identifier of the cached service.</param>
    /// <param name="instance">The cached service instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceIdentifier"/> or <paramref name="instance"/> is <see langword="null"/>.</exception>
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
