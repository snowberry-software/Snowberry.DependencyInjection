using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Lookup;

/// <inheritdoc cref="IServiceScopeFactory"/>
internal class DefaultServiceScopeFactory : IServiceScopeFactory
{
    private readonly ServiceContainer _serviceContainer;

    public DefaultServiceScopeFactory(ServiceContainer serviceContainer)
    {
        _serviceContainer = serviceContainer;
    }

    /// <inheritdoc/>
    public IScope CreateScope()
    {
        var scope = new DefaultServiceScopeProvider(_serviceContainer, isRootScope: false);
        return scope;
    }
}
