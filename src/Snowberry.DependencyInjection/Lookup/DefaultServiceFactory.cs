using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Helper;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

namespace Snowberry.DependencyInjection.Lookup;

/// <inheritdoc cref="IServiceFactory"/>
public partial class DefaultServiceFactory : IServiceFactory
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public DefaultServiceFactory(ServiceContainer serviceContainer)
    {
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    public object CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type, IServiceProvider serviceProvider, Type[]? genericTypeArguments = null)
    {
        _ = type ?? throw new ArgumentNullException(nameof(type));

        if (type.IsInterface || type.IsAbstract)
            throw new InvalidServiceImplementationType(type, $"Cannot instantiate abstract classes or interfaces! ({type.FullName})!");

        if (type.IsGenericTypeDefinition)
        {
            if (genericTypeArguments == null)
                throw new ArgumentNullException(nameof(genericTypeArguments));

            type = type.MakeGenericType(genericTypeArguments);
        }

        var metadata = GetTypeMetadata(type);

        if (metadata.ConstructorInvoker == null)
        {
            ThrowHelper.ThrowInvalidConstructor(type);
            return null!;
        }

        // Use cached parameter metadata - no reflection needed!
        var parameters = metadata.ConstructorInvoker.Parameters;
        object?[] args = new object?[parameters.Length];

        var keyedServiceProvider = serviceProvider as IKeyedServiceProvider;
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            bool hasDefaultValue = param.DefaultValue != null;

            object? paramValue = null;

            if (param.ServiceKey != null)
            {
                if (keyedServiceProvider == null)
                    throw new InvalidOperationException($"The service provider does not support keyed service resolution (type=`{type.FullName}`, param=`{param.Name}`, paramType=`{param.ParameterType}`)!");

                paramValue = !hasDefaultValue ? keyedServiceProvider.GetRequiredKeyedService(param.ParameterType, param.ServiceKey) : keyedServiceProvider.GetKeyedService(param.ParameterType, param.ServiceKey);
            }
            else
                paramValue = !hasDefaultValue ? serviceProvider.GetRequiredService(param.ParameterType) : serviceProvider.GetService(param.ParameterType);

            paramValue ??= param.DefaultValue;

            args[i] = paramValue;
        }

        // Use compiled expression invoker instead of reflection
        object instance = metadata.ConstructorInvoker.Invoker(args);

        // Inject properties using cached metadata
        var properties = metadata.InjectableProperties;
        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];

            object? propertyValue = null;
            if (property.ServiceKey != null)
            {
                if (keyedServiceProvider == null)
                    throw new InvalidOperationException($"The service provider does not support keyed service resolution (type=`{type.FullName}`, property=`{property.PropertyName}`, propertyType=`{property.PropertyType}`)!");

                propertyValue = keyedServiceProvider.GetKeyedService(property.PropertyType, property.ServiceKey);
            }
            else
                propertyValue = serviceProvider.GetService(property.PropertyType);

            if (property.IsRequired && propertyValue is null)
                throw new ServiceTypeNotRegistered(property.PropertyType, $"The required service for the property `{property.PropertyName}` is not registered!");

            property.SetValue(instance, propertyValue);
        }

        return instance;
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
    public T CreateInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(IServiceProvider serviceProvider, Type[]? genericTypeArguments = null)
    {
        object service = CreateInstance(typeof(T), serviceProvider, genericTypeArguments);

        if (service is T type)
            return type;

        ThrowHelper.ThrowInvalidServiceImplementationCast(typeof(T), service.GetType());
        return default!;
    }

    /// <inheritdoc/>
    public ConstructorInfo? GetConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type instanceType)
    {
        var metadata = GetTypeMetadata(instanceType);
        return metadata.Constructor;
    }

    /// <summary>
    /// Gets or creates comprehensive metadata for a type including constructor, parameters, and properties.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "The BuildTypeMetadata delegate correctly preserves all DynamicallyAccessedMembers annotations through the GetOrAdd call.")]
    private static TypeMetadata GetTypeMetadata([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        return _typeMetadataCache.GetOrAdd(type, BuildTypeMetadata);
    }

    /// <summary>
    /// Builds comprehensive metadata for a type.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "ParameterInfo.ParameterType from constructor parameters inherits the DynamicallyAccessedMembers requirements from the declaring type's constructor analysis.")]
    private static TypeMetadata BuildTypeMetadata([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        // Find the best constructor
        var constructor = SelectConstructor(type);

        ConstructorInvokerInfo? constructorInfo = null;
        if (constructor != null)
        {
            var parameters = constructor.GetParameters();

            // Cache parameter metadata to avoid reflection on every instantiation
            var parameterInfos = new ParameterCacheInfo[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                // Suppress IL2072: ParameterInfo.ParameterType doesn't have DynamicallyAccessedMembers annotation in the framework
                // The actual types come from the constructor which does have the proper annotations via GetTypeMetadata
                parameterInfos[i] = new ParameterCacheInfo(
                    param.ParameterType,
                    param.Name,
                    param.HasDefaultValue ? param.DefaultValue : null,
                    param.GetCustomAttribute<FromKeyedServicesAttribute>()?.ServiceKey
                );
            }

            // Create parameter: object?[] args
            var argsParameter = Expression.Parameter(typeof(object?[]), "args");

            // Create expressions to extract and cast each argument from the array
            var argumentExpressions = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                // args[i]
                var arrayAccess = Expression.ArrayIndex(argsParameter, Expression.Constant(i));

                // (ParameterType)args[i]
                argumentExpressions[i] = Expression.Convert(arrayAccess, paramType);
            }

            // new T(arg0, arg1, ...)
            var newExpression = Expression.New(constructor, argumentExpressions);

            // Convert to object if needed
            var convertExpression = Expression.Convert(newExpression, typeof(object));

            // Compile: (object?[] args) => (object)new T((T0)args[0], (T1)args[1], ...)
            var invoker = Expression.Lambda<Func<object?[], object>>(convertExpression, argsParameter).Compile();

            constructorInfo = new ConstructorInvokerInfo(invoker, parameterInfos);
        }

        // Cache injectable properties
        var injectableProperties = type.GetProperties()
            .Where(p => p.SetMethod != null && p.GetCustomAttribute<InjectAttribute>() != null)
            .Select(p => new PropertyCacheInfo(p))
            .ToArray();

        return new TypeMetadata(constructor, constructorInfo, injectableProperties);
    }

    /// <summary>
    /// Selects the best constructor for a type.
    /// </summary>
    private static ConstructorInfo? SelectConstructor([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type instanceType)
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
    }

    /// <summary>
    /// Comprehensive cached metadata for a type including constructor and property information.
    /// </summary>
    private sealed class TypeMetadata
    {
        public TypeMetadata(ConstructorInfo? constructor, ConstructorInvokerInfo? constructorInvoker, PropertyCacheInfo[] injectableProperties)
        {
            Constructor = constructor;
            ConstructorInvoker = constructorInvoker;
            InjectableProperties = injectableProperties;
        }

        public ConstructorInfo? Constructor { get; }

        public ConstructorInvokerInfo? ConstructorInvoker { get; }

        public PropertyCacheInfo[] InjectableProperties { get; }
    }

    /// <summary>
    /// Cached information about a constructor including compiled invoker and parameter metadata.
    /// </summary>
    private sealed class ConstructorInvokerInfo(Func<object?[], object> invoker, ParameterCacheInfo[] parameters)
    {
        public Func<object?[], object> Invoker { get; } = invoker;

        public ParameterCacheInfo[] Parameters { get; } = parameters;
    }

    /// <summary>
    /// Cached metadata about a constructor parameter to avoid reflection on every instantiation.
    /// </summary>
    private sealed class ParameterCacheInfo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type parameterType,
        string? name,
        object? defaultValue,
        object? serviceKey)
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type ParameterType { get; } = parameterType;

        public string? Name { get; } = name;

        public object? ServiceKey { get; } = serviceKey;

        public object? DefaultValue { get; } = defaultValue;
    }

    /// <summary>
    /// Cached metadata about an injectable property including compiled setter.
    /// </summary>
    private sealed class PropertyCacheInfo
    {
        private readonly Action<object, object?> _compiledSetter;

        public PropertyCacheInfo(PropertyInfo propertyInfo)
        {
            PropertyType = propertyInfo.PropertyType;
            PropertyName = propertyInfo.Name;

            var fromKeyedAttribute = propertyInfo.GetCustomAttribute<FromKeyedServicesAttribute>();
            ServiceKey = fromKeyedAttribute?.ServiceKey;

            var injectAttribute = propertyInfo.GetCustomAttribute<InjectAttribute>()
                ?? throw new InvalidOperationException($"Property must have {nameof(InjectAttribute)}!");

            IsRequired = injectAttribute.IsRequired;

            _compiledSetter = CompilePropertySetter(propertyInfo);
        }

        private static Action<object, object?> CompilePropertySetter(PropertyInfo propertyInfo)
        {
            // Create parameters: (object instance, object? value)
            var instanceParameter = Expression.Parameter(typeof(object), "instance");
            var valueParameter = Expression.Parameter(typeof(object), "value");

            // Cast instance to declaring type (always required)
            var instanceCast = Expression.Convert(instanceParameter, propertyInfo.DeclaringType!);

            // Cast value to property type
            // Use TypeAs for reference types (handles null better), Convert for value types (required for unboxing)
            Expression valueCast;
            if (propertyInfo.PropertyType.IsValueType)
            {
                // Value types need Convert for proper unboxing
                valueCast = Expression.Convert(valueParameter, propertyInfo.PropertyType);
            }
            else
            {
                // Reference types can use TypeAs which is slightly more efficient
                valueCast = Expression.TypeAs(valueParameter, propertyInfo.PropertyType);
            }

            // instance.Property = value
            var propertyAccess = Expression.Property(instanceCast, propertyInfo);
            var assignExpression = Expression.Assign(propertyAccess, valueCast);

            // Compile: (object instance, object? value) => ((DeclaringType)instance).Property = (PropertyType)value
            return Expression.Lambda<Action<object, object?>>(assignExpression, instanceParameter, valueParameter).Compile();
        }

        public void SetValue(object instance, object? value)
        {
            _compiledSetter(instance, value);
        }

        public Type PropertyType { get; }

        public string PropertyName { get; }

        public object? ServiceKey { get; }

        public bool IsRequired { get; }
    }
}
