using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the Tier 3 opt-in frozen mode: <c>Freeze()</c> locks registrations and switches to the inlining
/// resolver pipeline, which must produce results identical to the mutable pipeline.
/// </summary>
public class FrozenContainerTests
{
    public interface ILeaf { }

    private sealed class Leaf : ILeaf { }

    public interface IMid { ILeaf Leaf { get; } }

    private sealed class Mid : IMid
    {
        public Mid(ILeaf leaf) => Leaf = leaf;

        public ILeaf Leaf { get; }
    }

    public interface IRoot { IMid Mid { get; } }

    private sealed class Root : IRoot
    {
        public Root(IMid mid) => Mid = mid;

        public IMid Mid { get; }
    }

    public interface IDisposableService { bool Disposed { get; } }

    private sealed class DisposableService : IDisposableService, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void IsFrozen_IsTrueOnlyAfterFreeze()
    {
        using var container = new ServiceContainer();
        Assert.False(container.IsFrozen);

        container.Freeze();
        Assert.True(container.IsFrozen);

        container.Freeze(); // idempotent
        Assert.True(container.IsFrozen);
    }

    [Fact]
    public void Register_AfterFreeze_Throws()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<ILeaf, Leaf>();
        container.Freeze();

        Assert.Throws<ServiceRegistryReadOnlyException>(() => container.RegisterTransient<IMid, Mid>());
    }

    [Fact]
    public void Unregister_AfterFreeze_Throws()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<ILeaf, Leaf>();
        container.Freeze();

        Assert.Throws<ServiceRegistryReadOnlyException>(() => container.UnregisterService<ILeaf>(serviceKey: null, out _));
    }

    [Fact]
    public void Freeze_WithMissingDependency_ThrowsValidationException()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<IRoot, Root>(); // IMid / ILeaf intentionally missing

        Assert.Throws<ServiceValidationException>(() => container.Freeze());
        Assert.False(container.IsFrozen); // not frozen when validation fails
    }

    [Fact]
    public void Freeze_WithValidateFalse_SkipsValidation()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<IRoot, Root>();

        container.Freeze(validate: false); // does not throw despite missing deps
        Assert.True(container.IsFrozen);
    }

    [Fact]
    public void Frozen_TransientChain_ResolvesGraphAndIsNewEachTime()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<ILeaf, Leaf>();
        container.RegisterTransient<IMid, Mid>();
        container.RegisterTransient<IRoot, Root>();
        container.Freeze();

        var first = container.GetRequiredService<IRoot>();
        var second = container.GetRequiredService<IRoot>();

        Assert.NotNull(first.Mid);
        Assert.NotNull(first.Mid.Leaf);
        Assert.IsType<Mid>(first.Mid);
        Assert.IsType<Leaf>(first.Mid.Leaf);
        Assert.NotSame(first, second);        // transient
        Assert.NotSame(first.Mid, second.Mid);
    }

    [Fact]
    public void Frozen_Singleton_ReturnsSameInstance()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<ILeaf, Leaf>();
        container.RegisterSingleton<IMid, Mid>();
        container.Freeze();

        var a = container.GetRequiredService<IMid>();
        var b = container.GetRequiredService<IMid>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Frozen_Scoped_SameWithinScope_DifferentAcrossScopes()
    {
        using var container = new ServiceContainer();
        container.RegisterScoped<ILeaf, Leaf>();
        container.Freeze();

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var a1 = scope1.ServiceProvider.GetRequiredService<ILeaf>();
        var a2 = scope1.ServiceProvider.GetRequiredService<ILeaf>();
        var b1 = scope2.ServiceProvider.GetRequiredService<ILeaf>();

        Assert.Same(a1, a2);
        Assert.NotSame(a1, b1);
    }

    [Fact]
    public void Frozen_DisposableTransient_IsTrackedAndDisposed()
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<IDisposableService, DisposableService>();
        container.Freeze();

        IDisposableService service;
        using (var scope = container.CreateScope())
        {
            service = scope.ServiceProvider.GetRequiredService<IDisposableService>();
            Assert.False(service.Disposed);
        }

        Assert.True(service.Disposed); // disposable transient (not inlined) tracked + disposed at scope teardown
    }

    [Fact]
    public void Frozen_ResultsMatchMutable()
    {
        static IRoot Resolve(bool freeze)
        {
            var c = new ServiceContainer();
            c.RegisterTransient<ILeaf, Leaf>();
            c.RegisterTransient<IMid, Mid>();
            c.RegisterTransient<IRoot, Root>();
            if (freeze)
                c.Freeze();
            return c.GetRequiredService<IRoot>();
        }

        var mutable = Resolve(freeze: false);
        var frozen = Resolve(freeze: true);

        Assert.IsType<Root>(mutable);
        Assert.IsType<Root>(frozen);
        Assert.IsType<Mid>(frozen.Mid);
        Assert.IsType<Leaf>(frozen.Mid.Leaf);
    }
}
