using System.Collections.Concurrent;
using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;

namespace Snowberry.DependencyInjection.Lookup;

public partial class DefaultServiceFactory
{
    private static readonly ConcurrentDictionary<Type, ConstructorInfo?> _constructorCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyCacheInfo[]> _injectablePropertiesCache = new();

    /// <inheritdoc/>
    public object CreateInstance(Type type, Type[]? genericTypeParameters = null)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));

        return CreateInstance(type, scope: null, genericTypeParameters);
    }

    /// <inheritdoc/>
    public T CreateInstance<T>(Type[]? genericTypeParameters = null)
    {
        return CreateInstance<T>(scope: null, genericTypeParameters);
    }

    /// <inheritdoc/>
    public T CreateInstance<T>(IScope? scope, Type[]? genericTypeParameters = null)
    {
        object service = CreateInstance(typeof(T), scope, genericTypeParameters)!;

        if (service is T type)
            return type;

        ThrowHelper.ThrowInvalidServiceImplementationCast(typeof(T), service.GetType());
        return default!;
    }

    /// <inheritdoc/>
    public ConstructorInfo? GetConstructor(Type instanceType)
    {
        return _constructorCache.GetOrAdd(instanceType, type =>
        {
            var constructors = instanceType.GetConstructors();

            if (constructors.Length == 1)
                return constructors[0];

            // Check for preferred constructor.
            for (int i = 0; i < constructors.Length; i++)
            {
                var constructor = constructors[i];

                if (constructor.GetCustomAttribute<PreferredConstructorAttribute>() != null)
                    return constructor;
            }

            // Otherwise get the constructor with the largest number of parameters.
            return constructors
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();
        });
    }

    /// <inheritdoc/>
    public object CreateInstance(Type type, IScope? scope, Type[]? genericTypeParameters = null)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));

        if (type.IsInterface || type.IsAbstract)
            throw new InvalidServiceImplementationType(type, $"Cannot instantiate abstract classes or interfaces! ({type.FullName})!");

        if (type.IsGenericTypeDefinition)
        {
            if (genericTypeParameters == null)
                throw new ArgumentNullException(nameof(genericTypeParameters));

            type = type.MakeGenericType(genericTypeParameters);
        }

        var constructor = GetConstructor(type);

        if (constructor == null)
        {
            if (type.IsValueType)
                return CreateBuiltInType(type);
        }
        else
        {
            var parameters = constructor.GetParameters().AsSpan();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                object? serviceKey = null;
                var keyedServiceAttribute = param.GetCustomAttribute<FromKeyedServicesAttribute>();

                if (keyedServiceAttribute != null)
                    serviceKey = keyedServiceAttribute.ServiceKey;

                args[i] = GetInstanceFromServiceType(parameters[i].ParameterType, scope, serviceKey);
            }

            object? instance = constructor.Invoke(args);

            var properties = _injectablePropertiesCache.GetOrAdd(type, t =>
                    [.. t.GetProperties()
                        .Where(p => p.SetMethod != null && p.GetCustomAttribute<InjectAttribute>() != null)
                        .Select(p => new PropertyCacheInfo(p))]);

            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var keyedServiceAttribute = property.FromKeyedServicesAttribute;

                object? propertyValue = null;
                if (keyedServiceAttribute != null)
                    propertyValue = GetOptionalKeyedService(property.PropertyInfo.PropertyType, keyedServiceAttribute.ServiceKey, scope);
                else
                    propertyValue = GetOptionalService(property.PropertyInfo.PropertyType, scope);

                if (property.InjectAttribute.IsRequired && propertyValue is null)
                    throw new ServiceTypeNotRegistered(property.PropertyInfo.PropertyType, $"The required service for the property `{property.PropertyInfo.Name}` is not registered!");

                property.PropertyInfo.SetValue(instance, propertyValue);
            }

            return instance;
        }

        ThrowHelper.ThrowInvalidConstructor(type);
        return null!;
    }

    private readonly struct PropertyCacheInfo
    {
        public PropertyCacheInfo(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
            FromKeyedServicesAttribute = propertyInfo.GetCustomAttribute<FromKeyedServicesAttribute>();
            InjectAttribute = propertyInfo.GetCustomAttribute<InjectAttribute>() ?? throw new InvalidOperationException($"Property must have {nameof(Abstractions.Attributes.InjectAttribute)}!");
        }

        public PropertyInfo PropertyInfo { get; }

        public FromKeyedServicesAttribute? FromKeyedServicesAttribute { get; }

        public InjectAttribute InjectAttribute { get; }
    }
}
