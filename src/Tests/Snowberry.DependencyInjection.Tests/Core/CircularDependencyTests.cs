using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the Tier 2 compiled resolver graph's cycle detection. A dependency cycle is detected when the
/// graph is built (on first resolve) and surfaces as a <see cref="CircularDependencyException"/> instead of a
/// stack overflow.
/// </summary>
public class CircularDependencyTests
{
    // Mutual cycle: CycleA -> CycleB -> CycleA.
    private sealed class CycleA
    {
        public CycleA(CycleB b) => B = b;

        public CycleB B { get; }
    }

    private sealed class CycleB
    {
        public CycleB(CycleA a) => A = a;

        public CycleA A { get; }
    }

    // Direct self cycle: SelfCycle -> SelfCycle.
    private sealed class SelfCycle
    {
        public SelfCycle(SelfCycle self) => Self = self;

        public SelfCycle Self { get; }
    }

    [Fact]
    public void Resolve_WithMutualCircularDependency_ThrowsCircularDependencyException()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<CycleA>();
        container.RegisterTransient<CycleB>();

        var exception = Assert.Throws<CircularDependencyException>(() => container.GetService<CycleA>());

        Assert.Contains(typeof(CycleA), exception.DependencyPath);
        Assert.Contains(typeof(CycleB), exception.DependencyPath);
    }

    [Fact]
    public void Resolve_WithDirectSelfCircularDependency_ThrowsCircularDependencyException()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<SelfCycle>();

        var exception = Assert.Throws<CircularDependencyException>(() => container.GetService<SelfCycle>());

        Assert.Equal(typeof(SelfCycle), exception.ServiceType);
    }

    [Fact]
    public void Resolve_WithMutualCircularDependency_FromBothEntryPoints_ThrowsConsistently()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<CycleA>();
        container.RegisterTransient<CycleB>();

        Assert.Throws<CircularDependencyException>(() => container.GetService<CycleA>());
        Assert.Throws<CircularDependencyException>(() => container.GetService<CycleB>());
    }
}
