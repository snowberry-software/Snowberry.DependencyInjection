using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Snowberry.DependencyInjection.Helper;

/// <summary>
/// Produces the constructor arguments for <see cref="ReflectionConstructor.Invoke{TArguments}(int, TArguments)"/>
/// by index.
/// </summary>
internal interface IConstructorArguments
{
    // Implemented by structs so the generic call is specialized per provider and needs no delegate per argument.

    /// <summary>
    /// Produces the argument at <paramref name="index"/>. Called exactly once per index, in ascending order.
    /// </summary>
    /// <param name="index">The zero-based parameter index.</param>
    /// <returns>The argument value.</returns>
    object? GetArgument(int index);
}

#if NET8_0_OR_GREATER
/// <summary>
/// Inline storage for up to <see cref="Capacity"/> constructor arguments.
/// </summary>
[InlineArray(Capacity)]
internal struct ArgumentBuffer
{
    // Lives on the stack, so interpreted activation of constructors with 5 to 8 parameters allocates no array.

    /// <summary>The number of arguments the buffer holds.</summary>
    public const int Capacity = 8;

    private object? _element0;
}
#endif

/// <summary>
/// Invokes a constructor through reflection, the interpreted tier of activation. Exceptions thrown by the
/// constructor surface unchanged (never wrapped in <see cref="TargetInvocationException"/>), matching a compiled
/// <c>new T(...)</c>.
/// </summary>
/// <remarks>
/// Uses <c>ConstructorInvoker</c> on .NET and <see cref="ConstructorInfo.Invoke(object[])"/> on .NET Standard;
/// both work under Native AOT.
/// </remarks>
internal sealed class ReflectionConstructor
{
    // ConstructorInvoker needs no argument array for up to four arguments.

#if NET8_0_OR_GREATER
    private readonly ConstructorInvoker _invoker;
#else
    private readonly ConstructorInfo _constructor;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionConstructor"/> class.
    /// </summary>
    /// <param name="constructor">The constructor to invoke.</param>
    public ReflectionConstructor(ConstructorInfo constructor)
    {
#if NET8_0_OR_GREATER
        _invoker = ConstructorInvoker.Create(constructor);
#else
        _constructor = constructor;
#endif
    }

    /// <summary>
    /// Invokes the constructor with <paramref name="count"/> arguments taken from <paramref name="arguments"/>,
    /// evaluated left to right before the constructor runs.
    /// </summary>
    /// <typeparam name="TArguments">The argument provider type.</typeparam>
    /// <param name="count">The number of constructor parameters.</param>
    /// <param name="arguments">The provider of the argument values.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke<TArguments>(int count, TArguments arguments)
        where TArguments : struct, IConstructorArguments
    {
        // C# evaluates call arguments left to right, so each overload below preserves argument order.
        switch (count)
        {
            case 0:
                return Invoke();
            case 1:
                return Invoke(arguments.GetArgument(0));
            case 2:
                return Invoke(arguments.GetArgument(0), arguments.GetArgument(1));
            case 3:
                return Invoke(arguments.GetArgument(0), arguments.GetArgument(1), arguments.GetArgument(2));
            case 4:
                return Invoke(arguments.GetArgument(0), arguments.GetArgument(1), arguments.GetArgument(2), arguments.GetArgument(3));
        }

#if NET8_0_OR_GREATER
        if (count <= ArgumentBuffer.Capacity)
        {
            var buffer = default(ArgumentBuffer);
            Span<object?> span = buffer;
            span = span.Slice(0, count);

            for (int i = 0; i < span.Length; i++)
                span[i] = arguments.GetArgument(i);

            return Invoke(span);
        }

        var heapArguments = new object?[count];
        for (int i = 0; i < heapArguments.Length; i++)
            heapArguments[i] = arguments.GetArgument(i);

        return Invoke(heapArguments.AsSpan());
#else
        var heapArguments = new object?[count];
        for (int i = 0; i < heapArguments.Length; i++)
            heapArguments[i] = arguments.GetArgument(i);

        return Invoke(heapArguments);
#endif
    }

    /// <summary>Invokes a parameterless constructor.</summary>
    /// <returns>The constructed instance.</returns>
    public object Invoke()
    {
        try
        {
#if NET8_0_OR_GREATER
            return _invoker.Invoke();
#else
            return _constructor.Invoke([]);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }

    /// <summary>Invokes a constructor with one argument.</summary>
    /// <param name="arg1">The first argument.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(object? arg1)
    {
        try
        {
#if NET8_0_OR_GREATER
            return _invoker.Invoke(arg1);
#else
            return _constructor.Invoke([arg1]);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }

    /// <summary>Invokes a constructor with two arguments.</summary>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(object? arg1, object? arg2)
    {
        try
        {
#if NET8_0_OR_GREATER
            return _invoker.Invoke(arg1, arg2);
#else
            return _constructor.Invoke([arg1, arg2]);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }

    /// <summary>Invokes a constructor with three arguments.</summary>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <param name="arg3">The third argument.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(object? arg1, object? arg2, object? arg3)
    {
        try
        {
#if NET8_0_OR_GREATER
            return _invoker.Invoke(arg1, arg2, arg3);
#else
            return _constructor.Invoke([arg1, arg2, arg3]);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }

    /// <summary>Invokes a constructor with four arguments.</summary>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <param name="arg3">The third argument.</param>
    /// <param name="arg4">The fourth argument.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(object? arg1, object? arg2, object? arg3, object? arg4)
    {
        try
        {
#if NET8_0_OR_GREATER
            return _invoker.Invoke(arg1, arg2, arg3, arg4);
#else
            return _constructor.Invoke([arg1, arg2, arg3, arg4]);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Invokes a constructor with any number of arguments.</summary>
    /// <param name="arguments">The arguments, one per constructor parameter.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(Span<object?> arguments)
    {
        try
        {
            return _invoker.Invoke(arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }
#else
    /// <summary>Invokes a constructor with any number of arguments.</summary>
    /// <param name="arguments">The arguments, one per constructor parameter.</param>
    /// <returns>The constructed instance.</returns>
    public object Invoke(object?[] arguments)
    {
        try
        {
            return _constructor.Invoke(arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw Unwrap(exception);
        }
    }
#endif

    /// <summary>
    /// Rethrows the exception thrown by the invoked member with its original stack trace.
    /// </summary>
    /// <param name="exception">The reflection wrapper exception.</param>
    /// <returns>Never returns; declared so callers can write <c>throw Unwrap(...)</c>.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Exception Unwrap(TargetInvocationException exception)
    {
        ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
        return exception;
    }
}

/// <summary>
/// Sets a property through reflection, the interpreted tier of property injection. Exceptions thrown by the
/// setter surface unchanged.
/// </summary>
internal sealed class ReflectionPropertySetter
{
#if NET8_0_OR_GREATER
    private readonly MethodInvoker _invoker;
#else
    private readonly PropertyInfo _property;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionPropertySetter"/> class.
    /// </summary>
    /// <param name="property">The property to set; it must have a setter.</param>
    public ReflectionPropertySetter(PropertyInfo property)
    {
#if NET8_0_OR_GREATER
        _invoker = MethodInvoker.Create(property.SetMethod!);
#else
        _property = property;
#endif
    }

    /// <summary>
    /// Assigns <paramref name="value"/> to the property of <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The instance whose property is set.</param>
    /// <param name="value">The value to assign; <see langword="null"/> assigns the default value of a value-type property.</param>
    public void SetValue(object instance, object? value)
    {
        try
        {
#if NET8_0_OR_GREATER
            _invoker.Invoke(instance, value);
#else
            _property.SetValue(instance, value);
#endif
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw ReflectionConstructor.Unwrap(exception);
        }
    }
}
