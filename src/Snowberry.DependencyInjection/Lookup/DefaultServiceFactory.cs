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
    private static readonly ConcurrentDictionary<Type, TypeMetadata> s_TypeMetadataCache = new();

    // Cached constructors used by the Tier 2 node compiler to bake lazy "required dependency not registered" throws.
    private static readonly ConstructorInfo s_ServiceNotRegisteredCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type) })!;

    private static readonly ConstructorInfo s_ServiceNotRegisteredWithMessageCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type), typeof(string) })!;

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultServiceFactory"/> class.
    /// </summary>
    /// <param name="serviceContainer">The service container the factory creates instances for.</param>
    public DefaultServiceFactory(ServiceContainer serviceContainer)
    {
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Generic type parameters cannot be statically analyzed. Ensure all types passed have the required public constructors and properties.")]
    [RequiresDynamicCode("Constructing generic types requires dynamic code generation.")]
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
    /// Compiles a delegate that constructs an instance of <paramref name="type"/>, resolving each constructor
    /// argument and <see cref="InjectAttribute"/> property through the resolver supplied by <paramref name="resolveChild"/>.
    /// </summary>
    /// <param name="type">The implementation type to construct. When it is a generic type definition, <paramref name="genericTypeArguments"/> closes it.</param>
    /// <param name="genericTypeArguments">The generic type arguments used to close <paramref name="type"/> when it is a generic type definition; otherwise <see langword="null"/>.</param>
    /// <param name="resolveChild">Supplies the resolver for each dependency, or <see langword="null"/> when the dependency is not registered.</param>
    /// <param name="shouldInline">Optional callback that selects dependencies to construct inline; <see langword="null"/> resolves every dependency through <paramref name="resolveChild"/>.</param>
    /// <returns>A delegate that produces a constructed instance for a given <see cref="DefaultServiceScopeProvider"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>, or <paramref name="type"/> is a generic type definition and <paramref name="genericTypeArguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidServiceImplementationType"><paramref name="type"/> is an interface or abstract class.</exception>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Closes an open generic via MakeGenericType and compiles the resolver with Expression.Compile. Reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresDynamicCode]; AOT consumers must register closed generic types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "MakeGenericType closes an already-registered implementation type whose members are preserved via the descriptor's [DynamicallyAccessedMembers] ImplementationType annotation.")]
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
                    s_ServiceNotRegisteredWithMessageCtor,
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
    /// Builds the expression for a single dependency typed as <paramref name="targetType"/>: the resolved value
    /// when <paramref name="child"/> is supplied, the <paramref name="defaultValue"/> when one exists, otherwise
    /// a throw of <see cref="ServiceTypeNotRegistered"/>.
    /// </summary>
    /// <param name="child">The resolver for the dependency, or <see langword="null"/> when it is not registered.</param>
    /// <param name="targetType">The type the resulting expression is converted to.</param>
    /// <param name="defaultValue">The default value to use when <paramref name="child"/> is <see langword="null"/>; <see langword="null"/> when the dependency is required.</param>
    /// <param name="scopeParameter">The scope parameter passed to the resolver.</param>
    /// <returns>An <see cref="Expression"/> producing the dependency value as <paramref name="targetType"/>.</returns>
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
        return Expression.Throw(Expression.New(s_ServiceNotRegisteredCtor, Expression.Constant(targetType, typeof(Type))), targetType);
    }

    /// <summary>
    /// Builds the expression that supplies the constructor argument described by <paramref name="parameter"/>,
    /// either by constructing an inlinable dependency directly or by resolving it through
    /// <paramref name="resolveChild"/>.
    /// </summary>
    /// <param name="parameter">Cached metadata for the constructor parameter being supplied.</param>
    /// <param name="scopeParameter">The scope parameter passed to the resolvers.</param>
    /// <param name="resolveChild">Supplies the resolver for the dependency.</param>
    /// <param name="shouldInline">Callback selecting dependencies to construct inline, or <see langword="null"/> to always resolve through <paramref name="resolveChild"/>.</param>
    /// <param name="inlineVisiting">Tracks the types currently being inlined to detect cycles; may be <see langword="null"/>.</param>
    /// <returns>An <see cref="Expression"/> producing the argument value as the parameter type.</returns>
    /// <exception cref="CircularDependencyException">A cycle is detected among the inlined dependencies.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The inline type originates from a registered descriptor's ImplementationType, which is annotated [DynamicallyAccessedMembers(PublicConstructors | PublicProperties)]. The shouldInline delegate boundary erases the static annotation, but the constructor and property members are preserved.")]
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
    /// Builds the construction expression for an inlinable dependency of type <paramref name="inlineType"/>,
    /// recursively inlining its own inlinable dependencies.
    /// </summary>
    /// <param name="inlineType">The implementation type to construct inline; must satisfy <see cref="IsInlinableTransientImplementation"/>.</param>
    /// <param name="scopeParameter">The scope parameter passed to the resolvers.</param>
    /// <param name="resolveChild">Supplies the resolver for each dependency.</param>
    /// <param name="shouldInline">Callback selecting which dependencies to construct inline.</param>
    /// <param name="inlineVisiting">Tracks the types currently being inlined to detect cycles.</param>
    /// <returns>A <see cref="NewExpression"/> that constructs <paramref name="inlineType"/>.</returns>
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
    /// Determines whether <paramref name="closedImplementationType"/> can be constructed directly: it has a
    /// usable public constructor, does not implement <see cref="IDisposable"/>, and has no
    /// <see cref="InjectAttribute"/> properties.
    /// </summary>
    /// <param name="closedImplementationType">The closed implementation type to test.</param>
    /// <returns><see langword="true"/> if the type can be constructed directly; otherwise, <see langword="false"/>.</returns>
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
    /// Gets the construct-time dependencies (constructor parameters and <see cref="InjectAttribute"/> properties)
    /// of <paramref name="closedImplementationType"/>.
    /// </summary>
    /// <param name="closedImplementationType">The closed implementation type to inspect.</param>
    /// <param name="dependencies">When this method returns, contains the dependencies of the type, or an empty list when the type has no usable constructor.</param>
    /// <returns><see langword="true"/> if the type has a usable public constructor; otherwise, <see langword="false"/>.</returns>
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
    /// Gets the cached <see cref="TypeMetadata"/> for the specified type, building and caching it on first access.
    /// </summary>
    /// <param name="type">The type whose metadata is requested.</param>
    /// <returns>The cached metadata describing the type's constructor and injectable properties.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "The BuildTypeMetadata delegate correctly preserves all DynamicallyAccessedMembers annotations through the GetOrAdd call.")]
    private static TypeMetadata GetTypeMetadata([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        return s_TypeMetadataCache.GetOrAdd(type, BuildTypeMetadata);
    }

    /// <summary>
    /// Builds the <see cref="TypeMetadata"/> for the specified type, including its selected constructor, a
    /// compiled invoker, cached parameter metadata, and injectable properties.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A new <see cref="TypeMetadata"/> describing the type.</returns>
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
    /// Resolves a single constructor argument from the provider, honoring keyed and default-value handling.
    /// </summary>
    /// <param name="param">Cached metadata for the constructor parameter to resolve.</param>
    /// <param name="declaringType">The type declaring the constructor, used for diagnostic messages.</param>
    /// <param name="serviceProvider">The provider used to resolve non-keyed services.</param>
    /// <param name="keyedServiceProvider">The provider used to resolve keyed services, or <see langword="null"/> when keyed resolution is unsupported.</param>
    /// <returns>The resolved argument value, or the parameter's default value when the service is not registered.</returns>
    /// <exception cref="InvalidOperationException">The parameter requires a keyed service but <paramref name="keyedServiceProvider"/> is <see langword="null"/>.</exception>
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
    /// Selects the constructor to use for the specified type: the only public constructor, the one marked with
    /// <see cref="PreferredConstructorAttribute"/>, or otherwise the public constructor with the most parameters.
    /// </summary>
    /// <param name="instanceType">The type whose constructor is selected.</param>
    /// <returns>The selected <see cref="ConstructorInfo"/>, or <see langword="null"/> when the type has no public constructor.</returns>
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

        /// <summary>Gets the cached metadata for the selected constructor's parameters.</summary>
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
        Type parameterType,
        string? name,
        object? defaultValue,
        object? serviceKey)
    {
        // No [DynamicallyAccessedMembers]: ParameterType is only used as a service-lookup key and for
        // Expression.Convert — never as a reflection target — so no member-preservation is required.
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
        /// Gets the compiled setter that assigns the property value, as an <see cref="Action{T1, T2}"/>.
        /// </summary>
        public Action<object, object?> Setter => _compiledSetter;

        public Type PropertyType { get; }

        public string PropertyName { get; }

        public object? ServiceKey { get; }

        public bool IsRequired { get; }
    }
}
