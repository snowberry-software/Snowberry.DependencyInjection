namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// One half of a mutual dependency cycle: <see cref="CycleA"/> needs <see cref="CycleB"/>, which needs <see cref="CycleA"/>.
/// </summary>
public sealed class CycleA
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CycleA"/> class.
    /// </summary>
    /// <param name="b">The other half of the cycle.</param>
    public CycleA(CycleB b) => B = b;

    /// <summary>
    /// Gets the other half of the cycle.
    /// </summary>
    public CycleB B { get; }
}

/// <summary>
/// The other half of the mutual dependency cycle that starts at <see cref="CycleA"/>.
/// </summary>
public sealed class CycleB
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CycleB"/> class.
    /// </summary>
    /// <param name="a">The other half of the cycle.</param>
    public CycleB(CycleA a) => A = a;

    /// <summary>
    /// Gets the other half of the cycle.
    /// </summary>
    public CycleA A { get; }
}
