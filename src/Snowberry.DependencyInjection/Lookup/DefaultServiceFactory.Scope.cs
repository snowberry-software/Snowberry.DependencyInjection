using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Lookup;

public partial class DefaultServiceFactory
{
    /// <inheritdoc/>
    public object? GetService(Type serviceType, IScope? scope)
    {
        return GetKeyedService(serviceType: serviceType, serviceKey: null, scope: scope);
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey, IScope? scope)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        lock (_lock)
        {
            var descriptor = ServiceDescriptorReceiver.GetOptionalServiceDescriptor(serviceType, serviceKey);

            if (descriptor == null)
                return null;

            return GetInstanceFromDescriptor(descriptor, scope, serviceType, serviceKey);
        }
    }
}
