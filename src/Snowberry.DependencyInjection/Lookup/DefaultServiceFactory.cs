using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Helper;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;

namespace Snowberry.DependencyInjection.Lookup;

/// <inheritdoc cref="IServiceFactory"/>
/// <remarks>
/// Supports Native AOT: on runtimes that do not compile dynamic code, instances are always constructed through
/// reflection.
/// </remarks>
public partial class DefaultServiceFactory : IServiceFactory
{
    // Activation is tiered (see RuntimeCompilation): a type is first constructed through reflection, and an
    // expression-compiled delegate takes over from the second activation on.

    private static readonly ConcurrentDictionary<Type, TypeMetadata> s_TypeMetadataCache = new();

    // Cached constructors used by the compiled node to bake lazy "required dependency not registered" throws.
    private static readonly ConstructorInfo s_ServiceNotRegisteredCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type) })!;

    private static readonly ConstructorInfo s_ServiceNotRegisteredWithMessageCtor =
        typeof(ServiceTypeNotRegistered).GetConstructor(new[] { typeof(Type), typeof(string) })!;

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

        if (metadata.Constructor == null)
        {
            ThrowHelper.ThrowInvalidConstructor(type);
            return null!;
        }

        // Each constructor argument is resolved inline from the provider, with no per-resolve object?[] array.
        object instance = metadata.Activate(serviceProvider);

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
    /// Builds the interpreted (reflection-based) construct delegate for <paramref name="type"/>: each constructor
    /// argument and <see cref="InjectAttribute"/> property is resolved through the resolver supplied by
    /// <paramref name="resolveChild"/>, and the instance is created through reflection. Its observable behavior
    /// matches <see cref="CompileNode"/>.
    /// </summary>
    /// <param name="type">The implementation type to construct. When it is a generic type definition, <paramref name="genericTypeArguments"/> closes it.</param>
    /// <param name="genericTypeArguments">The generic type arguments used to close <paramref name="type"/> when it is a generic type definition; otherwise <see langword="null"/>.</param>
    /// <param name="resolveChild">Supplies the resolver for each dependency, or <see langword="null"/> when the dependency is not registered.</param>
    /// <param name="closedType">When this method returns, contains the closed implementation type the delegate constructs.</param>
    /// <returns>A delegate that produces a constructed instance for a given <see cref="DefaultServiceScopeProvider"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>, or <paramref name="type"/> is a generic type definition and <paramref name="genericTypeArguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidServiceImplementationType"><paramref name="type"/> is an interface or abstract class.</exception>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Closes an open generic via MakeGenericType. Reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresDynamicCode]; AOT consumers must register closed generic types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "MakeGenericType closes an already-registered implementation type whose members are preserved via the descriptor's [DynamicallyAccessedMembers] ImplementationType annotation.")]
    internal Func<DefaultServiceScopeProvider, object> BuildInterpretedNode(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        Type[]? genericTypeArguments,
        ChildResolverFactory resolveChild,
        out Type closedType)
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

        closedType = type;

        var metadata = GetTypeMetadata(type);
        if (metadata.Constructor == null)
        {
            ThrowHelper.ThrowInvalidConstructor(type);
            return null!;
        }

        // Resolve the children in the same order as CompileNode (constructor parameters, then properties), so
        // cycle detection and resolver caching observe the same build sequence.
        var parameters = metadata.Parameters;
        var arguments = parameters.Length == 0 ? [] : new Func<DefaultServiceScopeProvider, object?>[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            arguments[i] = BuildArgumentSource(resolveChild(parameter.ParameterType, parameter.ServiceKey), parameter);
        }

        var properties = metadata.InjectableProperties;
        InterpretedProperty[] interpretedProperties = [];
        if (properties.Length > 0)
        {
            var list = new List<InterpretedProperty>(properties.Length);
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var child = resolveChild(property.PropertyType, property.ServiceKey);

                if (child == null && property.IsRequired)
                {
                    // Construct runs first, then this throws exactly as the compiled node does; later properties are never reached.
                    list.Add(new InterpretedProperty(property, child: null, missingRequired: true));
                    break;
                }

                list.Add(new InterpretedProperty(property, child, missingRequired: false));
            }

            interpretedProperties = list.ToArray();
        }

        return new InterpretedNode(metadata.ReflectionConstructor, arguments, interpretedProperties).Construct;
    }

    /// <summary>
    /// Builds the interpreted source of a single constructor argument: the resolved value when
    /// <paramref name="child"/> is supplied, the parameter's default value when one exists, otherwise a throw of
    /// <see cref="ServiceTypeNotRegistered"/> when the argument is evaluated.
    /// </summary>
    /// <param name="child">The resolver for the dependency, or <see langword="null"/> when it is not registered.</param>
    /// <param name="parameter">Cached metadata for the constructor parameter.</param>
    /// <returns>A delegate producing the argument value for a given scope.</returns>
    private static Func<DefaultServiceScopeProvider, object?> BuildArgumentSource(Func<DefaultServiceScopeProvider, object?>? child, ParameterCacheInfo parameter)
    {
        if (child != null)
            return child;

        object? defaultValue = parameter.DefaultValue;
        if (defaultValue != null)
            return _ => defaultValue;

        var parameterType = parameter.ParameterType;
        return _ => throw new ServiceTypeNotRegistered(parameterType);
    }

    /// <summary>
    /// Compiles a delegate that constructs an instance of <paramref name="type"/>, resolving each constructor
    /// argument and <see cref="InjectAttribute"/> property through the resolver supplied by <paramref name="resolveChild"/>.
    /// </summary>
    /// <param name="type">The implementation type to construct. When it is a generic type definition, <paramref name="genericTypeArguments"/> closes it.</param>
    /// <param name="genericTypeArguments">The generic type arguments used to close <paramref name="type"/> when it is a generic type definition; otherwise <see langword="null"/>.</param>
    /// <param name="resolveChild">Supplies the resolver for each dependency, or <see langword="null"/> when the dependency is not registered.</param>
    /// <param name="buildPath">The chain of service identifiers currently being built; inlined nodes are recorded here so a cycle that closes through a delegate-resolved dependency reports the full path.</param>
    /// <param name="shouldInline">Optional callback that selects dependencies to construct inline; <see langword="null"/> resolves every dependency through <paramref name="resolveChild"/>.</param>
    /// <param name="maxInlineDepth">The maximum inlining depth: a negative value inlines without limit, while a positive value inlines at most that many levels deep. Ignored when <paramref name="shouldInline"/> is <see langword="null"/>.</param>
    /// <returns>A delegate that produces a constructed instance for a given <see cref="DefaultServiceScopeProvider"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>, or <paramref name="type"/> is a generic type definition and <paramref name="genericTypeArguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidServiceImplementationType"><paramref name="type"/> is an interface or abstract class.</exception>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Closes an open generic via MakeGenericType and compiles the resolver with Expression.Compile. Only called when RuntimeCompilation.IsSupported; reached from the resolve path, which implements the BCL IServiceProvider and so cannot carry [RequiresDynamicCode]; AOT consumers must register closed generic types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "MakeGenericType closes an already-registered implementation type whose members are preserved via the descriptor's [DynamicallyAccessedMembers] ImplementationType annotation.")]
    internal Func<DefaultServiceScopeProvider, object> CompileNode(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        Type[]? genericTypeArguments,
        ChildResolverFactory resolveChild,
        List<ServiceIdentifier> buildPath,
        Func<Type, object?, Type?>? shouldInline = null,
        int maxInlineDepth = -1)
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

        // Constructor arguments: each resolved via its captured child resolver, or constructed inline for a
        // simple transient child when inlining is enabled (see BuildArgumentExpression and maxInlineDepth).
        var parameters = metadata.Parameters;
        var argumentExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            argumentExpressions[i] = BuildArgumentExpression(parameters[i], scopeParameter, resolveChild, shouldInline, inlineVisiting: null, maxInlineDepth, buildPath);

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
        // services too, matching CreateInstance, which sets properties on the boxed instance.
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

            statements.Add(BuildPropertyAssignment(property, instance, child, scopeParameter));
        }

        statements.Add(instance); // return inst

        var body = Expression.Block(typeof(object), new[] { instance }, statements);
        return Expression.Lambda<Func<DefaultServiceScopeProvider, object>>(body, scopeParameter).Compile();
    }

    /// <summary>
    /// Builds the expression that injects one <see cref="InjectAttribute"/> property of the boxed
    /// <paramref name="instance"/>.
    /// </summary>
    /// <param name="property">Cached metadata for the property.</param>
    /// <param name="instance">The variable holding the boxed instance.</param>
    /// <param name="child">The resolver for the property's dependency, or <see langword="null"/> when it is optional and not registered.</param>
    /// <param name="scopeParameter">The scope parameter passed to <paramref name="child"/>.</param>
    /// <returns>The assignment expression.</returns>
    private static Expression BuildPropertyAssignment(PropertyCacheInfo property, ParameterExpression instance, Func<DefaultServiceScopeProvider, object?>? child, ParameterExpression scopeParameter)
    {
        var propertyType = property.PropertyType;

        // value = child(scope)  (registered)  OR  null  (optional + unregistered)
        Expression value = child != null
            ? Expression.Invoke(Expression.Constant(child), scopeParameter)
            : Expression.Constant(null, typeof(object));

        var declaringType = property.Property.DeclaringType!;
        if (declaringType.IsValueType)
        {
            // Unboxing a value-type instance yields a copy, so the setter delegate (which writes through the box)
            // is the only way to mutate the instance that is returned.
            return Expression.Invoke(Expression.Constant(property.CompiledSetter), instance, value);
        }

        // ((DeclaringType)inst).Property = (PropertyType)value, assigned directly instead of via a setter delegate.
        Expression typedValue;
        if (child == null && propertyType.IsValueType)
            typedValue = Expression.Default(propertyType); // optional + unregistered value type: default, like reflection
        else if (propertyType.IsValueType)
            typedValue = Expression.Convert(value, propertyType);
        else
            typedValue = Expression.TypeAs(value, propertyType);

        return Expression.Assign(Expression.Property(Expression.Convert(instance, declaringType), property.Property), typedValue);
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
            // (targetType) child(scope). Convert unboxes value types.
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
    /// <param name="maxInlineDepth">The maximum inlining depth: a negative value inlines without limit, a positive value inlines at most that many levels deep.</param>
    /// <param name="buildPath">The shared build path; an inlined node is recorded here for the duration of its subtree so a cycle that closes through a delegate-resolved dependency reports the full path.</param>
    /// <returns>An <see cref="Expression"/> producing the argument value as the parameter type.</returns>
    /// <exception cref="CircularDependencyException">A cycle is detected among the inlined dependencies.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The inline type originates from a registered descriptor's ImplementationType, which is annotated [DynamicallyAccessedMembers(PublicConstructors | PublicProperties)]. The shouldInline delegate boundary erases the static annotation, but the constructor and property members are preserved.")]
    private Expression BuildArgumentExpression(ParameterCacheInfo parameter, ParameterExpression scopeParameter, ChildResolverFactory resolveChild, Func<Type, object?, Type?>? shouldInline, List<ServiceIdentifier>? inlineVisiting, int maxInlineDepth, List<ServiceIdentifier> buildPath)
    {
        // Inline only while under the depth limit (negative = unlimited). Depth is the count of types already
        // being inlined on this path; one-level inlining (maxInlineDepth 1) inlines a root's direct children only.
        if (shouldInline != null && (maxInlineDepth < 0 || (inlineVisiting?.Count ?? 0) < maxInlineDepth))
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
                // Record the inlined node on the shared build path too, so a cycle closing through a
                // delegate-resolved grandchild (one-level inlining) reports the full path, not just the root.
                buildPath.Add(identifier);
                try
                {
                    var inlinedNew = BuildInlinedConstruct(inlineType, scopeParameter, resolveChild, shouldInline, inlineVisiting, maxInlineDepth, buildPath);
                    return Expression.Convert(inlinedNew, parameter.ParameterType);
                }
                finally
                {
                    inlineVisiting.RemoveAt(inlineVisiting.Count - 1);
                    buildPath.RemoveAt(buildPath.Count - 1);
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
    /// <param name="maxInlineDepth">The maximum inlining depth propagated to each argument: a negative value inlines without limit, a positive value inlines at most that many levels deep.</param>
    /// <param name="buildPath">The shared build path propagated to each argument for cross-node cycle detection.</param>
    /// <returns>A <see cref="NewExpression"/> that constructs <paramref name="inlineType"/>.</returns>
    private NewExpression BuildInlinedConstruct([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type inlineType, ParameterExpression scopeParameter, ChildResolverFactory resolveChild, Func<Type, object?, Type?> shouldInline, List<ServiceIdentifier> inlineVisiting, int maxInlineDepth, List<ServiceIdentifier> buildPath)
    {
        var metadata = GetTypeMetadata(inlineType);
        var constructor = metadata.Constructor!; // IsInlinableTransientImplementation guarantees a usable constructor

        var parameters = metadata.Parameters;
        var argumentExpressions = new Expression[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            argumentExpressions[i] = BuildArgumentExpression(parameters[i], scopeParameter, resolveChild, shouldInline, inlineVisiting, maxInlineDepth, buildPath);

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

        if (TypeHelper.IsDisposableType(closedImplementationType))
            return false;

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

        // Constructor parameters: required unless they carry a (non-null) default, the same DefaultValue != null
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
    /// Builds the <see cref="TypeMetadata"/> for the specified type: its selected constructor, cached parameter
    /// metadata, and injectable properties. Activation delegates are created on first use.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A new <see cref="TypeMetadata"/> describing the type.</returns>
    private static TypeMetadata BuildTypeMetadata([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        // Find the best constructor
        var constructor = SelectConstructor(type);

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
                    param.HasDefaultValue ? NormalizeDefaultValue(param.ParameterType, param.DefaultValue) : null,
                    param.GetCustomAttribute<FromKeyedServicesAttribute>()?.ServiceKey
                );
            }
        }

        // Cache injectable properties
        var injectableProperties = type.GetProperties()
            .Where(p => p.SetMethod != null && p.GetCustomAttribute<InjectAttribute>() != null)
            .Select(p => new PropertyCacheInfo(p))
            .ToArray();

        return new TypeMetadata(type, constructor, parameterInfos, injectableProperties);
    }

    /// <summary>
    /// Converts a parameter's metadata default value to the parameter type where metadata stores it as the
    /// underlying primitive (enum parameters), so reflection invocation accepts it like a compiled cast does.
    /// </summary>
    /// <param name="parameterType">The parameter type.</param>
    /// <param name="defaultValue">The default value read from metadata.</param>
    /// <returns>The default value typed as the parameter type when a conversion applies; otherwise <paramref name="defaultValue"/>.</returns>
    private static object? NormalizeDefaultValue(Type parameterType, object? defaultValue)
    {
        if (defaultValue == null)
            return null;

        var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        if (targetType.IsEnum && defaultValue.GetType() != targetType)
            return Enum.ToObject(targetType, defaultValue);

        return defaultValue;
    }

    /// <summary>
    /// Compiles the activator used by <see cref="CreateInstance(Type, IServiceProvider, Type[])"/> once a type
    /// has been activated often enough: <c>(IServiceProvider sp) =&gt; (object)new T((T0)ResolveConstructorArgument(p0, type, sp, sp as IKeyedServiceProvider), ...)</c>.
    /// </summary>
    /// <param name="metadata">The metadata of the type to activate; its constructor must be set.</param>
    /// <returns>The compiled activator.</returns>
    private static Func<IServiceProvider, object> CompileActivator(TypeMetadata metadata)
    {
        var constructor = metadata.Constructor!;
        var parameterInfos = metadata.Parameters;

        var spParameter = Expression.Parameter(typeof(IServiceProvider), "sp");
        var keyedProvider = Expression.Variable(typeof(IKeyedServiceProvider), "keyedSp");
        var declaringTypeConstant = Expression.Constant(metadata.Type, typeof(Type));
        var resolveMethod = typeof(DefaultServiceFactory).GetMethod(nameof(ResolveConstructorArgument), BindingFlags.NonPublic | BindingFlags.Static)!;

        var argumentExpressions = new Expression[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            // (ParameterType)ResolveConstructorArgument(parameterInfos[i], type, sp, keyedSp)
            var resolveCall = Expression.Call(
                resolveMethod,
                Expression.Constant(parameterInfos[i]),
                declaringTypeConstant,
                spParameter,
                keyedProvider);

            argumentExpressions[i] = Expression.Convert(resolveCall, parameterInfos[i].ParameterType);
        }

        // new T(arg0, arg1, ...)
        var newExpression = Expression.New(constructor, argumentExpressions);

        var body = Expression.Block(
            new[] { keyedProvider },
            Expression.Assign(keyedProvider, Expression.TypeAs(spParameter, typeof(IKeyedServiceProvider))),
            Expression.Convert(newExpression, typeof(object)));

        return Expression.Lambda<Func<IServiceProvider, object>>(body, spParameter).Compile();
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
        // the previous stable OrderByDescending(...).FirstOrDefault(), without the enumerator/sort allocation.
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
    /// Comprehensive cached metadata for a type including constructor and property information, plus the lazily
    /// created, tiered activator used by <see cref="CreateInstance(Type, IServiceProvider, Type[])"/>.
    /// </summary>
    private sealed class TypeMetadata
    {
        private ReflectionConstructor? _reflectionConstructor;
        private Func<IServiceProvider, object>? _compiledActivator;
        private int _activationsUntilCompile = RuntimeCompilation.CompileAfterInvocations;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeMetadata"/> class.
        /// </summary>
        /// <param name="type">The type the metadata describes.</param>
        /// <param name="constructor">The selected constructor, or <see langword="null"/> when the type has no public constructor.</param>
        /// <param name="parameters">The cached metadata of the constructor's parameters.</param>
        /// <param name="injectableProperties">The properties marked with <see cref="InjectAttribute"/>.</param>
        public TypeMetadata(Type type, ConstructorInfo? constructor, ParameterCacheInfo[] parameters, PropertyCacheInfo[] injectableProperties)
        {
            Type = type;
            Constructor = constructor;
            Parameters = parameters;
            InjectableProperties = injectableProperties;
        }

        /// <summary>Gets the type this metadata describes.</summary>
        public Type Type { get; }

        /// <summary>Gets the selected constructor, or <see langword="null"/> when the type has no public constructor.</summary>
        public ConstructorInfo? Constructor { get; }

        /// <summary>Gets the cached metadata for the selected constructor's parameters.</summary>
        public ParameterCacheInfo[] Parameters { get; }

        /// <summary>Gets the properties marked with <see cref="InjectAttribute"/> that have a setter.</summary>
        public PropertyCacheInfo[] InjectableProperties { get; }

        /// <summary>Gets the reflection invoker for <see cref="Constructor"/>, created on first use. <see cref="Constructor"/> must be set.</summary>
        public ReflectionConstructor ReflectionConstructor => _reflectionConstructor ??= new ReflectionConstructor(Constructor!);

        /// <summary>
        /// Creates an instance, resolving each constructor argument from <paramref name="serviceProvider"/>.
        /// Runs through reflection until the type has been activated often enough, then through a compiled delegate.
        /// </summary>
        /// <param name="serviceProvider">The provider used to resolve the constructor arguments.</param>
        /// <returns>The constructed instance.</returns>
        public object Activate(IServiceProvider serviceProvider)
        {
            var compiled = _compiledActivator;
            if (compiled != null)
                return compiled(serviceProvider);

            if (RuntimeCompilation.IsSupported && Interlocked.Decrement(ref _activationsUntilCompile) == 0)
            {
                compiled = CompileActivator(this);
                _compiledActivator = compiled;
                return compiled(serviceProvider);
            }

            return ReflectionConstructor.Invoke(Parameters.Length, new ProviderArguments(this, serviceProvider, serviceProvider as IKeyedServiceProvider));
        }
    }

    /// <summary>
    /// Supplies constructor arguments resolved from an <see cref="IServiceProvider"/> to <see cref="ReflectionConstructor"/>.
    /// </summary>
    /// <param name="metadata">The metadata of the type being activated.</param>
    /// <param name="serviceProvider">The provider that resolves unkeyed arguments.</param>
    /// <param name="keyedServiceProvider">The provider that resolves keyed arguments, or <see langword="null"/> when keyed resolution is unsupported.</param>
    private readonly struct ProviderArguments(TypeMetadata metadata, IServiceProvider serviceProvider, IKeyedServiceProvider? keyedServiceProvider) : IConstructorArguments
    {
        /// <inheritdoc/>
        public object? GetArgument(int index)
        {
            return ResolveConstructorArgument(metadata.Parameters[index], metadata.Type, serviceProvider, keyedServiceProvider);
        }
    }

    /// <summary>
    /// The interpreted construct delegate of a node: resolves the constructor arguments through the captured
    /// sources, invokes the constructor through reflection, then injects the properties.
    /// </summary>
    /// <param name="constructor">The reflection invoker of the selected constructor.</param>
    /// <param name="arguments">One source per constructor parameter, in parameter order.</param>
    /// <param name="properties">The property injection steps, in declaration order.</param>
    private sealed class InterpretedNode(ReflectionConstructor constructor, Func<DefaultServiceScopeProvider, object?>[] arguments, InterpretedProperty[] properties)
    {
        private readonly ReflectionConstructor _constructor = constructor;
        private readonly Func<DefaultServiceScopeProvider, object?>[] _arguments = arguments;
        private readonly InterpretedProperty[] _properties = properties;

        /// <summary>
        /// Constructs an instance for <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope the dependencies are resolved in.</param>
        /// <returns>The constructed instance.</returns>
        public object Construct(DefaultServiceScopeProvider scope)
        {
            object instance = _constructor.Invoke(_arguments.Length, new ScopeArguments(_arguments, scope));

            var properties = _properties;
            for (int i = 0; i < properties.Length; i++)
                properties[i].Inject(instance, scope);

            return instance;
        }
    }

    /// <summary>
    /// Supplies constructor arguments produced by per-argument sources to <see cref="ReflectionConstructor"/>.
    /// </summary>
    /// <param name="sources">One source per constructor parameter.</param>
    /// <param name="scope">The scope the sources resolve in.</param>
    private readonly struct ScopeArguments(Func<DefaultServiceScopeProvider, object?>[] sources, DefaultServiceScopeProvider scope) : IConstructorArguments
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object? GetArgument(int index)
        {
            return sources[index](scope);
        }
    }

    /// <summary>
    /// One property injection step of an <see cref="InterpretedNode"/>.
    /// </summary>
    /// <param name="property">The property to inject.</param>
    /// <param name="child">The resolver of the property's service, or <see langword="null"/> when it is not registered.</param>
    /// <param name="missingRequired"><see langword="true"/> when the property is required and its service is not registered.</param>
    private sealed class InterpretedProperty(PropertyCacheInfo property, Func<DefaultServiceScopeProvider, object?>? child, bool missingRequired)
    {
        private readonly PropertyCacheInfo _property = property;
        private readonly Func<DefaultServiceScopeProvider, object?>? _child = child;
        private readonly bool _missingRequired = missingRequired;

        /// <summary>
        /// Injects the property of <paramref name="instance"/>, or throws when it is required but not registered.
        /// </summary>
        /// <param name="instance">The constructed instance.</param>
        /// <param name="scope">The scope the dependency is resolved in.</param>
        /// <exception cref="ServiceTypeNotRegistered">The property is required and its service is not registered.</exception>
        public void Inject(object instance, DefaultServiceScopeProvider scope)
        {
            if (_missingRequired)
                throw new ServiceTypeNotRegistered(_property.PropertyType, $"The required service for the property `{_property.PropertyName}` is not registered!");

            _property.SetValueThroughReflection(instance, _child?.Invoke(scope));
        }
    }

    /// <summary>
    /// Cached metadata about a constructor parameter.
    /// </summary>
    /// <param name="parameterType">The parameter type.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value, or <see langword="null"/> when the parameter is required.</param>
    /// <param name="serviceKey">The key from <see cref="FromKeyedServicesAttribute"/>, or <see langword="null"/>.</param>
    private sealed class ParameterCacheInfo(
        Type parameterType,
        string? name,
        object? defaultValue,
        object? serviceKey)
    {
        // No [DynamicallyAccessedMembers]: ParameterType is only used as a service-lookup key and for
        // Expression.Convert, never as a reflection target, so no member-preservation is required.
        /// <summary>Gets the parameter type, which is the requested service type.</summary>
        public Type ParameterType { get; } = parameterType;

        /// <summary>Gets the parameter name.</summary>
        public string? Name { get; } = name;

        /// <summary>Gets the key from <see cref="FromKeyedServicesAttribute"/>, or <see langword="null"/> for an unkeyed dependency.</summary>
        public object? ServiceKey { get; } = serviceKey;

        /// <summary>Gets the default value passed when the service is not registered, or <see langword="null"/> when the parameter is required.</summary>
        public object? DefaultValue { get; } = defaultValue;
    }

    /// <summary>
    /// Cached metadata about an injectable property, with a reflection setter and a lazily compiled setter.
    /// </summary>
    private sealed class PropertyCacheInfo
    {
        private readonly ReflectionPropertySetter _reflectionSetter;
        private Action<object, object?>? _compiledSetter;
        private int _setsUntilCompile = RuntimeCompilation.CompileAfterInvocations;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyCacheInfo"/> class.
        /// </summary>
        /// <param name="propertyInfo">The property marked with <see cref="InjectAttribute"/>; it must have a setter.</param>
        /// <exception cref="InvalidOperationException"><paramref name="propertyInfo"/> is not marked with <see cref="InjectAttribute"/>.</exception>
        public PropertyCacheInfo(PropertyInfo propertyInfo)
        {
            Property = propertyInfo;
            PropertyType = propertyInfo.PropertyType;
            PropertyName = propertyInfo.Name;

            var fromKeyedAttribute = propertyInfo.GetCustomAttribute<FromKeyedServicesAttribute>();
            ServiceKey = fromKeyedAttribute?.ServiceKey;

            var injectAttribute = propertyInfo.GetCustomAttribute<InjectAttribute>()
                ?? throw new InvalidOperationException($"Property must have {nameof(InjectAttribute)}!");

            IsRequired = injectAttribute.IsRequired;

            _reflectionSetter = new ReflectionPropertySetter(propertyInfo);
        }

        /// <summary>
        /// Compiles <c>(object instance, object? value) =&gt; ((DeclaringType)instance).Property = (PropertyType)value</c>;
        /// a <see langword="null"/> value assigns the default of a value-type property.
        /// </summary>
        /// <param name="propertyInfo">The property to assign.</param>
        /// <returns>The compiled setter.</returns>
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
                // Value types need Convert for proper unboxing; null (optional + unregistered) assigns the default
                // value, as the reflection setter does, instead of failing to unbox.
                valueCast = Expression.Condition(
                    Expression.ReferenceEqual(valueParameter, Expression.Constant(null)),
                    Expression.Default(propertyInfo.PropertyType),
                    Expression.Convert(valueParameter, propertyInfo.PropertyType));
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

        /// <summary>
        /// Assigns the property, through reflection until it has been set often enough and through a compiled
        /// setter afterwards.
        /// </summary>
        /// <param name="instance">The instance whose property is set.</param>
        /// <param name="value">The value to assign.</param>
        public void SetValue(object instance, object? value)
        {
            var compiled = _compiledSetter;
            if (compiled != null)
            {
                compiled(instance, value);
                return;
            }

            if (RuntimeCompilation.IsSupported && Interlocked.Decrement(ref _setsUntilCompile) == 0)
            {
                CompiledSetter(instance, value);
                return;
            }

            _reflectionSetter.SetValue(instance, value);
        }

        /// <summary>
        /// Assigns the property through reflection.
        /// </summary>
        /// <param name="instance">The instance whose property is set.</param>
        /// <param name="value">The value to assign.</param>
        public void SetValueThroughReflection(object instance, object? value)
        {
            _reflectionSetter.SetValue(instance, value);
        }

        /// <summary>
        /// Gets the compiled setter that assigns the property value, compiling it on first access.
        /// </summary>
        public Action<object, object?> CompiledSetter => _compiledSetter ??= CompilePropertySetter(Property);

        /// <summary>Gets the reflected property.</summary>
        public PropertyInfo Property { get; }

        /// <summary>Gets the property type, which is the requested service type.</summary>
        public Type PropertyType { get; }

        /// <summary>Gets the property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the key from <see cref="FromKeyedServicesAttribute"/>, or <see langword="null"/> for an unkeyed dependency.</summary>
        public object? ServiceKey { get; }

        /// <summary>Gets a value indicating whether resolution fails when the service is not registered (<see cref="InjectAttribute.IsRequired"/>).</summary>
        public bool IsRequired { get; }
    }
}
