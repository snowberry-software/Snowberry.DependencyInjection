using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Lookup.Cache;

namespace Snowberry.DependencyInjection.Lookup;

public partial class DefaultServiceFactory : IScopedServiceFactory
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private List<IScopeServiceCacheEntry> _scopeCache = [];

    public DefaultServiceFactory(IServiceDescriptorReceiver serviceDescriptorReceiver)
    {
        ServiceDescriptorReceiver = serviceDescriptorReceiver;
    }

    /// <inheritdoc/>
    public void NotifyScopeCreated(IScope scope)
    {
        scope.OnDispose += Scope_OnDispose;
    }

    /// <inheritdoc/>
    public void NotifyScopeDisposed(IScope? scope)
    {
        lock (_lock)
        {
            // Remove all scope dependent service instances.
            for (int i = _scopeCache.Count - 1; i >= 0; i--)
            {
                var entry = _scopeCache[i];

                if (entry.Scope != scope)
                    continue;

                _scopeCache.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Tries to resolve the <paramref name="serviceType"/> instance for the given <paramref name="scope"/> and it will create and register a new one if it does not exist.
    /// </summary>
    /// <param name="scope">The current scope.</param>
    /// <param name="serviceDescriptor">The service descriptor.</param>
    /// <param name="requestedServiceType">The requested service type.</param>
    /// <param name="serviceKey">The service key.</param>
    /// <returns>The instance for the <paramref name="requestedServiceType"/>.</returns>
    private object LocateOrCreateScopeInstance(IScope? scope, IServiceDescriptor serviceDescriptor, Type requestedServiceType, object? serviceKey)
    {
        var serviceIdentifier = new ServiceIdentifier(serviceDescriptor.ServiceType, serviceKey);

        lock (_lock)
        {
            if (scope != null && scope.IsDisposed)
                throw new ObjectDisposedException(nameof(IScope));

            for (int i = _scopeCache.Count - 1; i >= 0; i--)
            {
                var entry = _scopeCache[i];

                if (entry.Scope != scope)
                    continue;

                if (!entry.ServiceIdentifier.Equals(serviceIdentifier))
                    continue;

                return entry.Instance;
            }
        }

        object? scopedInstance = serviceDescriptor.InstanceFactory?.Invoke(this, serviceKey) ?? CreateInstance(serviceDescriptor.ImplementationType, scope, requestedServiceType.GenericTypeArguments);

        if (scopedInstance.IsDisposable())
            if (scope != null)
                scope.RegisterDisposable(scopedInstance);
            else
                ServiceDescriptorReceiver.RegisterDisposable(scopedInstance);

        lock (_lock)
        {
            _scopeCache.Add(new ScopeServiceCacheEntry(
                scope,
                serviceIdentifier,
                scopedInstance));
        }

        return scopedInstance;
    }

    private void Scope_OnDispose(object? sender, EventArgs e)
    {
        if (sender is not IScope scope)
            return;

        NotifyScopeDisposed(scope);
        scope.OnDispose -= Scope_OnDispose;
    }

    protected object GetInstanceFromServiceType(Type serviceType, IScope? scope, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        var typeCode = Type.GetTypeCode(serviceType);
        if (serviceType.IsValueType && typeCode is not TypeCode.Empty and not TypeCode.Object)
            return CreateBuiltInType(serviceType);

        var descriptor = ServiceDescriptorReceiver.GetServiceDescriptor(serviceType, serviceKey);
        return GetInstanceFromDescriptor(descriptor, scope, serviceType, serviceKey);
    }

    protected object CreateBuiltInType(Type type)
    {
        var typeCode = Type.GetTypeCode(type);
        return typeCode switch
        {
            TypeCode.Empty => throw new NotImplementedException(),
            TypeCode.Object => new object(),
            TypeCode.Boolean => false,
            TypeCode.Byte => (byte)0,
            TypeCode.Char => (char)0,
            TypeCode.DateTime => DateTime.Now,
            TypeCode.DBNull => DBNull.Value,
            TypeCode.Decimal => (decimal)0,
            TypeCode.Double => 0D,
            TypeCode.Int16 => (short)0,
            TypeCode.Int32 => 0,
            TypeCode.Int64 => (long)0,
            TypeCode.SByte => (sbyte)0,
            TypeCode.Single => 0F,
            TypeCode.String => string.Empty,
            TypeCode.UInt16 => (ushort)0,
            TypeCode.UInt32 => (uint)0,
            TypeCode.UInt64 => (ulong)0,
            _ => Activator.CreateInstance(type)!
        };
    }

    protected object GetInstanceFromDescriptor(IServiceDescriptor serviceDescriptor, IScope? scope, Type requestedServiceType, object? serviceKey)
    {
        _ = serviceDescriptor ?? throw new ArgumentNullException(nameof(serviceDescriptor));

        switch (serviceDescriptor.Lifetime)
        {
            case ServiceLifetime.Singleton:

                // NOTE(VNC): Only register the disposable of the singleton if no explicit instance has been set before.
                if (serviceDescriptor.SingletonInstance == null)
                {
                    serviceDescriptor.SingletonInstance = serviceDescriptor.InstanceFactory?.Invoke(this, serviceKey) ?? CreateInstance(serviceDescriptor.ImplementationType, scope, requestedServiceType.GenericTypeArguments);

                    if (serviceDescriptor.SingletonInstance.IsDisposable())
                        ServiceDescriptorReceiver.RegisterDisposable(serviceDescriptor.SingletonInstance);
                }

                return serviceDescriptor.SingletonInstance;

            case ServiceLifetime.Transient:

                object? transientInstance = serviceDescriptor.InstanceFactory?.Invoke(this, serviceKey) ?? CreateInstance(serviceDescriptor.ImplementationType, scope, requestedServiceType.GenericTypeArguments);

                if (transientInstance.IsDisposable())
                {
                    if (scope != null)
                        scope.RegisterDisposable(transientInstance);
                    else
                        ServiceDescriptorReceiver.RegisterDisposable(transientInstance);
                }

                return transientInstance;

            case ServiceLifetime.Scoped:
                {
                    return LocateOrCreateScopeInstance(scope, serviceDescriptor, requestedServiceType, serviceKey);
                }

            default:
                return ThrowHelper.ThrowServiceLifetimeNotImplemented(serviceDescriptor.Lifetime);
        }
    }

    /// <inheritdoc/>
    public T? GetOptionalService<T>()
    {
        return GetOptionalService<T>(scope: null);
    }

    /// <inheritdoc/>
    public T GetService<T>()
    {
        return GetService<T>(scope: null);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return GetOptionalService(serviceType, scope: null);
    }

    /// <inheritdoc/>
    public object? GetOptionalService(Type serviceType)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return GetOptionalService(serviceType, scope: null);
    }

    /// <inheritdoc/>
    public object GetKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return GetKeyedService(serviceType, serviceKey, scope: null);
    }

    /// <inheritdoc/>
    public object? GetOptionalKeyedService(Type serviceType, object? serviceKey)
    {
        _ = serviceType ?? throw new ArgumentNullException(nameof(serviceType));

        return GetOptionalKeyedService(serviceType, serviceKey, scope: null);
    }

    /// <inheritdoc/>
    public T GetKeyedService<T>(object? serviceKey)
    {
        return GetKeyedService<T>(serviceKey, scope: null);
    }

    /// <inheritdoc/>
    public T? GetOptionalKeyedService<T>(object? serviceKey)
    {
        return GetOptionalKeyedService<T>(serviceKey, scope: null);
    }

    /// <summary>
    /// The receiver that will be used to resolve a <see cref="IServiceDescriptor"/>.
    /// </summary>
    public IServiceDescriptorReceiver ServiceDescriptorReceiver { get; }
}
