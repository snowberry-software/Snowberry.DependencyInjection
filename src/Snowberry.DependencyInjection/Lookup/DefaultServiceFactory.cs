using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Helper;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Implementation;

namespace Snowberry.DependencyInjection.Lookup;

/// <inheritdoc cref="IServiceFactory"/>
public partial class DefaultServiceFactory : IServiceFactory
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();

    // Cached constructors used by the Tier 2 node compiler to bake lazy "required dependency not registered" throws.
    private static readonly ConstructorInfo _serviceNotRegisteredCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type) })!;

    private static readonly ConstructorInfo _serviceNotRegisteredWithMessageCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type), typeof(string) })!;

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
    /// Tier 2: compiles a node that constructs <paramref name="type"/> by invoking the resolvers of its
    /// constructor arguments and <c>[Inject]</c> properties DIRECTLY (no per-argument re-dispatch by type). The
    /// child resolver for each dependency is obtained once, at compile time, from <paramref name="resolveChild"/>
    /// and baked into the expression as a constant; <c>null</c> from the callback means the dependency is
    /// unregistered, in which case the compiler bakes the exact runtime behavior — a <see cref="ServiceTypeNotRegistered"/>
    /// throw for a required dependency, or the default value for an optional one. Preserves the construction
    /// semantics of <see cref="CreateInstance(Type, IServiceProvider, Type[])"/> (constructor selection,
    /// keyed/default-value handling, property injection order, value-type unboxing) exactly.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    internal Func<DefaultServiceScopeProvider, object> CompileNode(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        Type[]? genericTypeArguments,
        ChildResolverFactory resolveChild,
        Func<Type, object?, Type?>? shouldInline = null)
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
        var constructor = metadata.Constructor;
        if (constructor == null)
        {
            ThrowHelper.ThrowInvalidConstructor(type);
            return null!;
        }

        var scopeParameter = Expression.Parameter(typeof(DefaultServiceScopeProvider), "scope");

        // Constructor arguments: each resolved via its captured child resolver (or, when frozen, an inlined
        // `new` for a simple transient child — see BuildArgumentExpression).
        var parameters = metadata.Parameters;
        var argumentExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            argumentExpressions[i] = BuildArgumentExpression(parameters[i], scopeParameter, resolveChild, shouldInline, inlineVisiting: null);

        var newExpression = Expression.New(constructor, argumentExpressions);

        var properties = metadata.InjectableProperties;
        if (properties.Length == 0)
        {
            // (object) new T(args)
            var simpleBody = Expression.Convert(newExpression, typeof(object));
            return Expression.Lambda<Func<DefaultServiceScopeProvider, object>>(simpleBody, scopeParameter).Compile();
        }

        // object inst = (object) new T(args);  <property assignments / required-null throw>;  return inst;
        // The instance is boxed once into `inst` so property injection works on the same object for value-type
        // services too — matching CreateInstance, which sets properties on the boxed instance.
        var instance = Expression.Variable(typeof(object), "inst");
        var statements = new List<Expression>(properties.Length + 2)
        {
            Expression.Assign(instance, Expression.Convert(newExpression, typeof(object)))
        };

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var child = resolveChild(property.PropertyType, property.ServiceKey);

            if (child == null && property.IsRequired)
            {
                // Construct ran above; now throw exactly as the runtime loop does for a required-null property.
                statements.Add(Expression.Throw(Expression.New(
                    _serviceNotRegisteredWithMessageCtor,
                    Expression.Constant(property.PropertyType, typeof(Type)),
                    Expression.Constant($"The required service for the property `{property.PropertyName}` is not registered!"))));
                break;
            }

            // value = child(scope)  (registered)  OR  null  (optional + unregistered)
            Expression value = child != null
                ? Expression.Invoke(Expression.Constant(child), scopeParameter)
                : Expression.Constant(null, typeof(object));

            // property.Setter(inst, value) — invoke the public Action so the expression avoids the private type.
            statements.Add(Expression.Invoke(Expression.Constant(property.Setter), instance, value));
        }

        statements.Add(instance); // return inst

        var body = Expression.Block(typeof(object), new[] { instance }, statements);
        return Expression.Lambda<Func<DefaultServiceScopeProvider, object>>(body, scopeParameter).Compile();
    }

    /// <summary>
    /// Builds the expression for a single constructor argument, typed as <paramref name="targetType"/>:
    /// registered → cast the child resolver's result; optional + unregistered → the (boxed) default value;
    /// required + unregistered → a lazily-thrown <see cref="ServiceTypeNotRegistered"/> (matching
    /// <c>GetRequiredService</c>). The optional/required split uses the existing <c>DefaultValue != null</c>
    /// proxy (an explicit <c>null</c> default is treated as required) — reproduced, not "fixed".
    /// </summary>
    private static Expression BuildDependencyExpression(Func<DefaultServiceScopeProvider, object?>? child, Type targetType, object? defaultValue, ParameterExpression scopeParameter)
    {
        if (child != null)
        {
            // (targetType) child(scope) — Convert unboxes value types.
            var invoke = Expression.Invoke(Expression.Constant(child), scopeParameter);
            return Expression.Convert(invoke, targetType);
        }

        if (defaultValue != null)
        {
            // Optional: (targetType) defaultValue (boxed → unbox/cast).
            return Expression.Convert(Expression.Constant(defaultValue, typeof(object)), targetType);
        }

        // Required + unregistered: throw ServiceTypeNotRegistered(targetType), typed as targetType so it fits the arg slot.
        return Expression.Throw(Expression.New(_serviceNotRegisteredCtor, Expression.Constant(targetType, typeof(Type))), targetType);
    }

    /// <summary>
    /// Builds a constructor-argument expression. In frozen mode (<paramref name="shouldInline"/> non-null), a
    /// simple inlinable transient dependency is constructed INLINE — its <c>new</c> is emitted recursively
    /// instead of a delegate invoke — eliminating the per-node hop. All other dependencies (scoped, singleton,
    /// disposable/property-bearing transients, factories, built-ins, unregistered) go through the captured child
    /// resolver exactly as in mutable mode.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The inline type is a registered implementation whose annotated metadata is preserved through the closure that produced it.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private Expression BuildArgumentExpression(ParameterCacheInfo parameter, ParameterExpression scopeParameter, ChildResolverFactory resolveChild, Func<Type, object?, Type?>? shouldInline, List<ServiceIdentifier>? inlineVisiting)
    {
        if (shouldInline != null)
        {
            var inlineType = shouldInline(parameter.ParameterType, parameter.ServiceKey);
            if (inlineType != null)
            {
                var identifier = new ServiceIdentifier(parameter.ParameterType, parameter.ServiceKey);
                inlineVisiting ??= new List<ServiceIdentifier>();

                // Backstop for a transient cycle reached with validation disabled (Freeze(validate: false)).
                // Normally Freeze() validates first and rejects cyclic graphs before freezing. The ordered list
                // gives the cycle path for the exception message.
                if (inlineVisiting.Contains(identifier))
                {
                    var path = new List<Type>(inlineVisiting.Count + 1);
                    for (int i = 0; i < inlineVisiting.Count; i++)
                        path.Add(inlineVisiting[i].ServiceType);
                    path.Add(parameter.ParameterType);
                    throw new CircularDependencyException(parameter.ParameterType, path);
                }

                inlineVisiting.Add(identifier);
                try
                {
                    var inlinedNew = BuildInlinedConstruct(inlineType, scopeParameter, resolveChild, shouldInline, inlineVisiting);
                    return Expression.Convert(inlinedNew, parameter.ParameterType);
                }
                finally
                {
                    inlineVisiting.RemoveAt(inlineVisiting.Count - 1);
                }
            }
        }

        var child = resolveChild(parameter.ParameterType, parameter.ServiceKey);
        return BuildDependencyExpression(child, parameter.ParameterType, parameter.DefaultValue, scopeParameter);
    }

    /// <summary>
    /// Recursively builds the <c>new T(...)</c> expression for an inlinable transient (no <c>[Inject]</c>
    /// properties, non-disposable, constructor-backed — guaranteed by <see cref="IsInlinableTransientImplementation"/>),
    /// inlining its inlinable transient children in turn. Pure construction: no property block, no disposal
    /// tracking (the type is non-disposable).
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The inline type is a registered implementation; its constructor metadata is resolved via the same annotated path as construction.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private NewExpression BuildInlinedConstruct([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type inlineType, ParameterExpression scopeParameter, ChildResolverFactory resolveChild, Func<Type, object?, Type?> shouldInline, List<ServiceIdentifier> inlineVisiting)
    {
        var metadata = GetTypeMetadata(inlineType);
        var constructor = metadata.Constructor!; // IsInlinableTransientImplementation guarantees a usable constructor

        var parameters = metadata.Parameters;
        var argumentExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            argumentExpressions[i] = BuildArgumentExpression(parameters[i], scopeParameter, resolveChild, shouldInline, inlineVisiting);

        return Expression.New(constructor, argumentExpressions);
    }

    /// <summary>
    /// Whether a closed implementation type is a "simple" transient that can be constructed inline in frozen
    /// mode: it has a usable public constructor, is NOT disposable (so it needs no per-instance disposal
    /// tracking), and has NO <c>[Inject]</c> properties (so its construction is a pure <c>new</c>).
    /// </summary>
    internal bool IsInlinableTransientImplementation([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type closedImplementationType)
    {
        if (closedImplementationType.IsInterface || closedImplementationType.IsAbstract)
            return false;

        if (typeof(IDisposable).IsAssignableFrom(closedImplementationType))
            return false;

#if NETCOREAPP
        if (typeof(IAsyncDisposable).IsAssignableFrom(closedImplementationType))
            return false;
#endif

        var metadata = GetTypeMetadata(closedImplementationType);
        return metadata.Constructor != null && metadata.InjectableProperties.Length == 0;
    }

    /// <summary>
    /// Exposes the construct-time dependencies (constructor parameters + <c>[Inject]</c> properties) of a closed
    /// implementation type for eager validation. Returns <c>false</c> when the type has no usable public
    /// constructor. Does not construct anything.
    /// </summary>
    internal bool TryGetDependencies(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type closedImplementationType,
        out IReadOnlyList<ServiceDependencyInfo> dependencies)
    {
        var metadata = GetTypeMetadata(closedImplementationType);
        if (metadata.Constructor == null)
        {
            dependencies = [];
            return false;
        }

        var list = new List<ServiceDependencyInfo>(metadata.Parameters.Length + metadata.InjectableProperties.Length);

        // Constructor parameters: required unless they carry a (non-null) default — the same DefaultValue != null
        // proxy used when resolving (an explicit null default is treated as required).
        foreach (var parameter in metadata.Parameters)
            list.Add(new ServiceDependencyInfo(parameter.ParameterType, parameter.ServiceKey, parameter.DefaultValue == null, parameter.Name, isProperty: false));

        // [Inject] properties: required per the attribute.
        foreach (var property in metadata.InjectableProperties)
            list.Add(new ServiceDependencyInfo(property.PropertyType, property.ServiceKey, property.IsRequired, property.PropertyName, isProperty: true));

        dependencies = list;
        return true;
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
        ParameterCacheInfo[] parameterInfos = [];
        if (constructor != null)
        {
            var parameters = constructor.GetParameters();

            // Cache parameter metadata to avoid reflection on every instantiation
            parameterInfos = new ParameterCacheInfo[parameters.Length];
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

        return new TypeMetadata(constructor, constructorInfo, parameterInfos, injectableProperties);
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
        public TypeMetadata(ConstructorInfo? constructor, ConstructorInvokerInfo? constructorInvoker, ParameterCacheInfo[] parameters, PropertyCacheInfo[] injectableProperties)
        {
            Constructor = constructor;
            ConstructorInvoker = constructorInvoker;
            Parameters = parameters;
            InjectableProperties = injectableProperties;
        }

        public ConstructorInfo? Constructor { get; }

        public ConstructorInvokerInfo? ConstructorInvoker { get; }

        /// <summary>Cached constructor-parameter metadata (used by the Tier 2 node compiler).</summary>
        public ParameterCacheInfo[] Parameters { get; }

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

        /// <summary>
        /// The compiled setter, exposed so the Tier 2 node compiler can invoke it via a public
        /// <see cref="Action{T1, T2}"/> constant (the expression never references this private type).
        /// </summary>
        public Action<object, object?> Setter => _compiledSetter;

        public Type PropertyType { get; }

        public string PropertyName { get; }

        public object? ServiceKey { get; }

        public bool IsRequired { get; }
    }
}
