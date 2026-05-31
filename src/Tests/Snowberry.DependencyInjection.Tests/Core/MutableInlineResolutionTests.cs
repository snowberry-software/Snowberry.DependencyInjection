using Snowberry.DependencyInjection.Abstractions.Extensions;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for the one-level transient inlining performed on the mutable (default) resolve path. The parent
/// node constructs a simple-transient child directly instead of through the child's resolver delegate; these
/// tests assert that this stays correct under re-registration, produces fresh instances, and never inlines a
/// disposable child (which must still be tracked and disposed).
/// </summary>
public class MutableInlineResolutionTests
{
    public interface IInlineChild
    {
        int Id { get; }
    }

    public sealed class InlineChildA : IInlineChild
    {
        public int Id => 1;
    }

    public sealed class InlineChildB : IInlineChild
    {
        public int Id => 2;
    }

    public sealed class InlineParent
    {
        public InlineParent(IInlineChild child)
        {
            Child = child;
        }

        public IInlineChild Child { get; }
    }

    public sealed class DisposableChild : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class ParentWithDisposableChild
    {
        public ParentWithDisposableChild(DisposableChild child)
        {
            Child = child;
        }

        public DisposableChild Child { get; }
    }

    [Fact]
    public void MutableInline_ReRegisteredChild_TakesEffectOnNextResolve()
    {
        // Arrange: parent has a simple-transient child that the mutable path inlines.
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
        container.RegisterTransient<IInlineChild, InlineChildA>();
        container.RegisterTransient<InlineParent>();

        // Act: first resolve compiles and caches the inlined parent resolver.
        var first = container.GetRequiredService<InlineParent>();

        // Overwrite the child registration; this must invalidate the inlined parent.
        container.RegisterTransient<IInlineChild, InlineChildB>();
        var second = container.GetRequiredService<InlineParent>();

        // Assert: the rebuilt parent constructs the newly-registered child implementation.
        Assert.Equal(1, first.Child.Id);
        Assert.Equal(2, second.Child.Id);
        Assert.IsType<InlineChildB>(second.Child);
    }

    [Fact]
    public void MutableInline_TransientChild_ProducesFreshInstancesEachResolve()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<IInlineChild, InlineChildA>();
        container.RegisterTransient<InlineParent>();

        // Act
        var first = container.GetRequiredService<InlineParent>();
        var second = container.GetRequiredService<InlineParent>();

        // Assert: inlining must not turn a transient child into a shared instance.
        Assert.NotSame(first, second);
        Assert.NotSame(first.Child, second.Child);
        Assert.Equal(1, first.Child.Id);
    }

    [Fact]
    public void MutableInline_InlinedChild_StillResolvableDirectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<IInlineChild, InlineChildA>();
        container.RegisterTransient<InlineParent>();

        // Act: resolving the parent (which inlines the child) must not break direct resolution of the child.
        _ = container.GetRequiredService<InlineParent>();
        var child = container.GetRequiredService<IInlineChild>();

        // Assert
        Assert.IsType<InlineChildA>(child);
    }

    [Fact]
    public void MutableInline_DisposableChild_IsNotInlinedAndStillDisposed()
    {
        // Arrange: a disposable child must be excluded from inlining so it is tracked and disposed.
        using var container = new ServiceContainer();
        container.RegisterTransient<DisposableChild>();
        container.RegisterTransient<ParentWithDisposableChild>();

        ParentWithDisposableChild parent;
        using (var scope = container.CreateScope())
        {
            parent = scope.ServiceProvider.GetRequiredService<ParentWithDisposableChild>();
            Assert.False(parent.Child.IsDisposed);
        }

        // Assert: disposing the scope disposed the (non-inlined, tracked) disposable child.
        Assert.True(parent.Child.IsDisposed);
    }
}
