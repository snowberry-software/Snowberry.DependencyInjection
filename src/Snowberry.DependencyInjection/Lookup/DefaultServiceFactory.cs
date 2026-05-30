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

        // Compiled invoker resolves each constructor argument inline from the provider — no per-resolve object?[] array.
        object instance = metadata.ConstructorInvoker.Invoke(serviceProvider);

        // Inject properties using cached metadata
        var properties = metadata.InjectableProperties;
        if (properties.Length == 0)
            return instance;

        var keyedServiceProvider = serviceProvider as IKeyedServiceProvider;
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

            // Compile an invoker that resolves each argument inline from the provider, so no object?[] array is
            // allocated per construction:
            //   (IServiceProvider sp) => { var keyedSp = sp as IKeyedServiceProvider;
            //                              return (object)new T( (T0)ResolveConstructorArgument(p0, type, sp, keyedSp), ... ); }
            var spParameter = Expression.Parameter(typeof(IServiceProvider), "sp");
            var keyedProvider = Expression.Variable(typeof(IKeyedServiceProvider), "keyedSp");
            var declaringTypeConstant = Expression.Constant(type, typeof(Type));
            var resolveMethod = typeof(DefaultServiceFactory).GetMethod(nameof(ResolveConstructorArgument), BindingFlags.NonPublic | BindingFlags.Static)!;

            var argumentExpressions = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                // (ParameterType)ResolveConstructorArgument(parameterInfos[i], type, sp, keyedSp)
                var resolveCall = Expression.Call(
                    resolveMethod,
                    Expression.Constant(parameterInfos[i]),
                    declaringTypeConstant,
                    spParameter,
                    keyedProvider);

                argumentExpressions[i] = Expression.Convert(resolveCall, parameters[i].ParameterType);
            }

            // new T(arg0, arg1, ...)
            var newExpression = Expression.New(constructor, argumentExpressions);

            var body = Expression.Block(
                new[] { keyedProvider },
                Expression.Assign(keyedProvider, Expression.TypeAs(spParameter, typeof(IKeyedServiceProvider))),
                Expression.Convert(newExpression, typeof(object)));

            var invoker = Expression.Lambda<Func<IServiceProvider, object>>(body, spParameter).Compile();

            constructorInfo = new ConstructorInvokerInfo(invoker);
        }

        // Cache injectable properties
        var injectableProperties = type.GetProperties()
            .Where(p => p.SetMethod != null && p.GetCustomAttribute<InjectAttribute>() != null)
            .Select(p => new PropertyCacheInfo(p))
            .ToArray();

        return new TypeMetadata(constructor, constructorInfo, injectableProperties);
    }

    /// <summary>
    /// Resolves a single constructor argument from the provider. Invoked by the compiled per-type invoker so
    /// arguments are resolved without allocating an <c>object?[]</c> per construction. Behavior (required vs.
    /// optional, keyed vs. non-keyed, default-value fallback) and the thrown exception/message are identical
    /// to the original inline resolution loop.
    /// </summary>
    private static object? ResolveConstructorArgument(ParameterCacheInfo param, Type declaringType, IServiceProvider serviceProvider, IKeyedServiceProvider? keyedServiceProvider)
    {
        bool hasDefaultValue = param.DefaultValue != null;

        object? paramValue;

        if (param.ServiceKey != null)
        {
            if (keyedServiceProvider == null)
                throw new InvalidOperationException($"The service provider does not support keyed service resolution (type=`{declaringType.FullName}`, param=`{param.Name}`, paramType=`{param.ParameterType}`)!");

            paramValue = !hasDefaultValue ? keyedServiceProvider.GetRequiredKeyedService(param.ParameterType, param.ServiceKey) : keyedServiceProvider.GetKeyedService(param.ParameterType, param.ServiceKey);
        }
        else
            paramValue = !hasDefaultValue ? serviceProvider.GetRequiredService(param.ParameterType) : serviceProvider.GetService(param.ParameterType);

        return paramValue ?? param.DefaultValue;
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

        // Otherwise get the constructor with the largest number of parameters. First-seen wins ties, matching
        // the previous stable OrderByDescending(...).FirstOrDefault() — without the enumerator/sort allocation.
        ConstructorInfo? best = null;
        int bestParameterCount = -1;

        for (int i = 0; i < constructors.Length; i++)
        {
            int parameterCount = constructors[i].GetParameters().Length;

            if (parameterCount > bestParameterCount)
            {
                bestParameterCount = parameterCount;
                best = constructors[i];
            }
        }

        return best;
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
    /// Cached compiled invoker that constructs the instance, resolving each argument inline from the provider.
    /// </summary>
    private sealed class ConstructorInvokerInfo(Func<IServiceProvider, object> invoker)
    {
        private readonly Func<IServiceProvider, object> _invoker = invoker;

        public object Invoke(IServiceProvider serviceProvider)
        {
            return _invoker(serviceProvider);
        }
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
