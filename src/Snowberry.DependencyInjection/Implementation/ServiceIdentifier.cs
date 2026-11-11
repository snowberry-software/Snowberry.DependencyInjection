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

    /// <inheritdoc/>
    public bool Equals(ServiceIdentifier other)
    {
        // Value-typed equality used by the default equality comparer, so the struct can be a dictionary key
        // without boxing to IServiceIdentifier on every lookup.
        if (ServiceKey == null)
            return other.ServiceKey == null && ServiceType == other.ServiceType;

        // Reference equality first: keys are typically the same string literal or boxed constant, which skips the
        // virtual Equals call.
        return other.ServiceKey != null
            && ServiceType == other.ServiceType
            && (ReferenceEquals(ServiceKey, other.ServiceKey) || ServiceKey.Equals(other.ServiceKey));
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
