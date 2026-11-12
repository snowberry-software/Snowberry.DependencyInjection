using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Enum used to test enum-typed constructor default values.
/// </summary>
public enum TierMode
{
    /// <summary>The first value.</summary>
    First = 1,

    /// <summary>The second value.</summary>
    Second = 2
}

/// <summary>
/// A service that is never registered in the tiered activation tests.
/// </summary>
public interface ITierMissing;

/// <summary>
/// A dependency without dependencies of its own.
/// </summary>
public class TierLeaf;

/// <summary>
/// A disposable dependency without dependencies of its own.
/// </summary>
public sealed class TierDisposableLeaf : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Dispose"/> has been called.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// A service whose optional constructor parameters have default values of several kinds.
/// </summary>
/// <param name="leaf">A required dependency.</param>
/// <param name="number">An optional value-type parameter.</param>
/// <param name="mode">An optional enum parameter.</param>
/// <param name="text">An optional reference-type parameter.</param>
/// <param name="optionalMode">An optional nullable enum parameter.</param>
public class TierWithDefaults(TierLeaf leaf, int number = 42, TierMode mode = TierMode.Second, string text = "hi", TierMode? optionalMode = TierMode.First)
{
    /// <summary>Gets the injected <see cref="TierLeaf"/>.</summary>
    public TierLeaf Leaf { get; } = leaf;

    /// <summary>Gets the received <c>number</c> argument.</summary>
    public int Number { get; } = number;

    /// <summary>Gets the received <c>mode</c> argument.</summary>
    public TierMode Mode { get; } = mode;

    /// <summary>Gets the received <c>text</c> argument.</summary>
    public string Text { get; } = text;

    /// <summary>Gets the received <c>optionalMode</c> argument.</summary>
    public TierMode? OptionalMode { get; } = optionalMode;
}

/// <summary>
/// A service with five constructor parameters, one of them disposable.
/// </summary>
/// <param name="a">The first dependency.</param>
/// <param name="b">The second dependency.</param>
/// <param name="c">The third dependency.</param>
/// <param name="d">The fourth dependency.</param>
/// <param name="e">The disposable dependency.</param>
public class TierFive(TierLeaf a, TierLeaf b, TierLeaf c, TierLeaf d, TierDisposableLeaf e)
{
    /// <summary>Gets the four <see cref="TierLeaf"/> dependencies, in parameter order.</summary>
    public TierLeaf[] Leaves { get; } = [a, b, c, d];

    /// <summary>Gets the disposable dependency.</summary>
    public TierDisposableLeaf Disposable { get; } = e;
}

/// <summary>
/// A service with nine constructor parameters.
/// </summary>
/// <param name="a">Dependency 1.</param>
/// <param name="b">Dependency 2.</param>
/// <param name="c">Dependency 3.</param>
/// <param name="d">Dependency 4.</param>
/// <param name="e">Dependency 5.</param>
/// <param name="f">Dependency 6.</param>
/// <param name="g">Dependency 7.</param>
/// <param name="h">Dependency 8.</param>
/// <param name="i">Dependency 9.</param>
public class TierNine(TierLeaf a, TierLeaf b, TierLeaf c, TierLeaf d, TierLeaf e, TierLeaf f, TierLeaf g, TierLeaf h, TierLeaf i)
{
    /// <summary>Gets the nine dependencies, in parameter order.</summary>
    public TierLeaf[] Leaves { get; } = [a, b, c, d, e, f, g, h, i];
}

/// <summary>
/// A service with a required dependency that is never registered.
/// </summary>
/// <param name="leaf">A registered dependency.</param>
/// <param name="missing">The unregistered dependency.</param>
public class TierNeedsMissing(TierLeaf leaf, ITierMissing missing)
{
    /// <summary>Gets the registered dependency.</summary>
    public TierLeaf Leaf { get; } = leaf;

    /// <summary>Gets the unregistered dependency; never reached because construction fails.</summary>
    public ITierMissing Missing { get; } = missing;
}

/// <summary>
/// A service whose constructor always throws <see cref="TierConstructorException"/>.
/// </summary>
public class TierThrowingConstructor
{
    /// <summary>
    /// Always throws.
    /// </summary>
    /// <exception cref="TierConstructorException">Always.</exception>
    public TierThrowingConstructor()
    {
        throw new TierConstructorException();
    }
}

/// <summary>
/// The exception thrown by <see cref="TierThrowingConstructor"/>.
/// </summary>
public sealed class TierConstructorException() : Exception("constructor failed");

/// <summary>
/// A service with required, optional and keyed <see cref="InjectAttribute"/> properties.
/// </summary>
public class TierWithProperties
{
    /// <summary>Gets or sets a required injected dependency.</summary>
    [Inject]
    public TierLeaf? Required { get; set; }

    /// <summary>Gets or sets an optional dependency that is never registered; its initializer value is overwritten.</summary>
    [Inject(false)]
    public ITierMissing? OptionalMissing { get; set; } = new TierMissingDefault();

    /// <summary>Gets or sets an optional value-type dependency that is never registered.</summary>
    [Inject(false)]
    public int OptionalValue { get; set; } = 7;

    /// <summary>Gets or sets an optional keyed dependency (key <c>"k"</c>).</summary>
    [Inject(false)]
    [FromKeyedServices("k")]
    public TierLeaf? Keyed { get; set; }

    /// <summary>Gets or sets a property that is not injected.</summary>
    public TierLeaf? NotInjected { get; set; }
}

/// <summary>
/// The initializer value of <see cref="TierWithProperties.OptionalMissing"/>.
/// </summary>
public class TierMissingDefault : ITierMissing;

/// <summary>
/// A service with a required <see cref="InjectAttribute"/> property that is never registered.
/// </summary>
public class TierRequiredPropertyMissing
{
    /// <summary>
    /// The number of instances constructed so far, across all tests.
    /// </summary>
    public static int Constructed;

    /// <summary>
    /// Initializes a new instance and increments <see cref="Constructed"/>.
    /// </summary>
    public TierRequiredPropertyMissing()
    {
        Interlocked.Increment(ref Constructed);
    }

    /// <summary>Gets or sets the required dependency that is never registered.</summary>
    [Inject]
    public ITierMissing? Missing { get; set; }
}

/// <summary>
/// A service with a keyed and an unkeyed constructor dependency of the same type.
/// </summary>
/// <param name="keyed">The dependency registered under key <c>"k"</c>.</param>
/// <param name="unkeyed">The unkeyed dependency.</param>
public class TierKeyedConsumer([FromKeyedServices("k")] TierLeaf keyed, TierLeaf unkeyed)
{
    /// <summary>Gets the keyed dependency.</summary>
    public TierLeaf Keyed { get; } = keyed;

    /// <summary>Gets the unkeyed dependency.</summary>
    public TierLeaf Unkeyed { get; } = unkeyed;
}

/// <summary>Scoped test service 0 of 12, used to fill a scope past its small-cache capacity.</summary>
public class TierScoped0;

/// <summary>Scoped test service 1 of 12.</summary>
public class TierScoped1;

/// <summary>Scoped test service 2 of 12.</summary>
public class TierScoped2;

/// <summary>Scoped test service 3 of 12.</summary>
public class TierScoped3;

/// <summary>Scoped test service 4 of 12.</summary>
public class TierScoped4;

/// <summary>Scoped test service 5 of 12.</summary>
public class TierScoped5;

/// <summary>Scoped test service 6 of 12.</summary>
public class TierScoped6;

/// <summary>Scoped test service 7 of 12.</summary>
public class TierScoped7;

/// <summary>Scoped test service 8 of 12.</summary>
public class TierScoped8;

/// <summary>Scoped test service 9 of 12.</summary>
public class TierScoped9;

/// <summary>Scoped test service 10 of 12.</summary>
public class TierScoped10;

/// <summary>Scoped test service 11 of 12.</summary>
public class TierScoped11;
