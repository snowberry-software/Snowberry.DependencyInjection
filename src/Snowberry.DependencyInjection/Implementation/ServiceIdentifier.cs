using System.Collections.Generic;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Implementation;

/// <inheritdoc cref="IServiceIdentifier"/>
public readonly struct ServiceIdentifier : IServiceIdentifier, IEquatable<ServiceIdentifier>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceIdentifier"/> struct for the default (non-keyed) registration.
    /// </summary>
    /// <param name="serviceType">The type of the service.</param>
    public ServiceIdentifier(Type serviceType)
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceIdentifier"/> struct for a keyed registration.
    /// </summary>
    /// <param name="serviceType">The type of the service.</param>
    /// <param name="serviceKey">The service key, or <see langword="null"/> for the default registration.</param>
    public ServiceIdentifier(Type serviceType, object? serviceKey)
    {
        ServiceType = serviceType;
        ServiceKey = serviceKey;
    }

    /// <inheritdoc/>
    public bool Equals(IServiceIdentifier? other)
    {
        if (other == null)
            return false;

        if (ServiceKey == null && other.ServiceKey == null)
            return ServiceType == other.ServiceType;

        if (ServiceKey != null && other.ServiceKey != null)
            return ServiceType == other.ServiceType && ServiceKey.Equals(other.ServiceKey);

        return false;
    }

    /// <summary>
    /// Value-typed equality used by <see cref="ServiceIdentifierComparer"/> so the struct can be a
    /// dictionary key without boxing to <see cref="IServiceIdentifier"/> on every lookup.
    /// </summary>
    public bool Equals(ServiceIdentifier other)
    {
        if (ServiceKey == null)
            return other.ServiceKey == null && ServiceType == other.ServiceType;

        return other.ServiceKey != null && ServiceType == other.ServiceType && ServiceKey.Equals(other.ServiceKey);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ServiceIdentifier identifier && Equals(identifier);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (ServiceKey == null)
            return ServiceType.GetHashCode();

#if NETCOREAPP
        return HashCode.Combine(ServiceType, ServiceKey);
#else
        unchecked
        {
            uint h1 = (uint)ServiceType.GetHashCode();
            uint h2 = (uint)ServiceKey.GetHashCode();
            return (int)(h1 ^ ((h2 << 16) | (h2 >> 16)));
        }
#endif
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        if (ServiceKey == null)
            return ServiceType.ToString();

        return $"({ServiceKey}, {ServiceType})";
    }

    /// <inheritdoc/>
    public Type ServiceType { get; }

    /// <inheritdoc/>
    public object? ServiceKey { get; }
}

/// <summary>
/// Value-type equality comparer for <see cref="ServiceIdentifier"/>. Supplying this to the lookup
/// dictionaries keeps the struct key from being boxed to <see cref="IServiceIdentifier"/> on every
/// <c>TryGetValue</c>/insert.
/// </summary>
internal sealed class ServiceIdentifierComparer : IEqualityComparer<ServiceIdentifier>
{
    public static readonly ServiceIdentifierComparer Instance = new();

    public bool Equals(ServiceIdentifier x, ServiceIdentifier y)
    {
        return x.Equals(y);
    }

    public int GetHashCode(ServiceIdentifier obj)
    {
        return obj.GetHashCode();
    }
}
