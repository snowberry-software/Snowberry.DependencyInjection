using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the eager <c>Validate</c> / <c>TryValidate</c> API: catches missing/circular dependencies up
/// front without constructing any instances.
/// </summary>
public class ServiceValidationTests
{
    public interface IDep { }

    private sealed class Dep : IDep { }

    public interface INeedsDep { }

    private sealed class NeedsDep : INeedsDep
    {
        public NeedsDep(IDep dep) => Dep = dep;

        public IDep Dep { get; }
    }

    private sealed class ConstructionCounter
    {
        public int Count;
    }

    private sealed class CountedService
    {
        public CountedService(ConstructionCounter counter) => counter.Count++;
    }

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

    [Fact]
    public void Validate_WithCompleteGraph_Succeeds()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<IDep, Dep>();
        container.RegisterTransient<INeedsDep, NeedsDep>();

        Assert.True(container.TryValidate(out var errors));
        Assert.Empty(errors);
        container.Validate(); // does not throw
    }

    [Fact]
    public void Validate_WithMissingRequiredDependency_ReportsAndThrows()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<INeedsDep, NeedsDep>(); // IDep intentionally not registered

        Assert.False(container.TryValidate(out var errors));
        var error = Assert.Single(errors);
        Assert.Equal(ServiceValidationErrorKind.MissingDependency, error.Kind);
        Assert.Equal(typeof(IDep), error.DependencyType);

        var exception = Assert.Throws<ServiceValidationException>(container.Validate);
        Assert.Single(exception.Errors);
    }

    [Fact]
    public void Validate_AfterRegisteringMissingDependency_Succeeds()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<INeedsDep, NeedsDep>();
        Assert.False(container.TryValidate(out _));

        container.RegisterTransient<IDep, Dep>();
        Assert.True(container.TryValidate(out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_DoesNotConstructInstances()
    {
        using var container = new ServiceContainer();
        var counter = new ConstructionCounter();
        container.RegisterSingleton(counter);
        container.RegisterSingleton<CountedService>();

        container.Validate();
        Assert.Equal(0, counter.Count); // build, not construct

        _ = container.GetRequiredService<CountedService>();
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public void Validate_WithCircularDependency_ReportsCycle()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<CycleA>();
        container.RegisterTransient<CycleB>();

        Assert.False(container.TryValidate(out var errors));
        Assert.Contains(errors, e => e.Kind == ServiceValidationErrorKind.CircularDependency);
    }
}
