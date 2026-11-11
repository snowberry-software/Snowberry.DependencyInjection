using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Resolution;

/// <summary>
/// Activation is tiered: the first activation of a node runs through reflection and later activations through a
/// compiled delegate. Every test here repeats the same operation across that boundary and asserts identical
/// behavior in both tiers.
/// </summary>
public class TieredActivationTests
{
    // Enough repetitions to cover the interpreted tier, the compiling invocation, and the compiled steady state.
    private const int Iterations = 4;

    [Fact]
    public void Transient_WithDefaultValues_BehavesTheSameInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierWithDefaults>();

        for (int i = 0; i < Iterations; i++)
        {
            var instance = container.GetRequiredService<TierWithDefaults>();

            Assert.NotNull(instance.Leaf);
            Assert.Equal(42, instance.Number);
            Assert.Equal(TierMode.Second, instance.Mode);
            Assert.Equal("hi", instance.Text);
            Assert.Equal(TierMode.First, instance.OptionalMode);
        }
    }

    [Fact]
    public void Transient_WithFiveAndNineArguments_SuppliesEveryArgumentInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierDisposableLeaf>();
        container.RegisterTransient<TierFive>();
        container.RegisterTransient<TierNine>();

        for (int i = 0; i < Iterations; i++)
        {
            var five = container.GetRequiredService<TierFive>();
            var nine = container.GetRequiredService<TierNine>();

            Assert.All(five.Leaves, Assert.NotNull);
            Assert.NotNull(five.Disposable);
            Assert.Equal(9, nine.Leaves.Distinct().Count());
        }
    }

    [Fact]
    public void Transient_DisposableDependency_IsTrackedInEveryTierAndNonDisposableIsNot()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierDisposableLeaf>();
        container.RegisterTransient<TierFive>();

        var disposables = new List<TierDisposableLeaf>();
        for (int i = 0; i < Iterations; i++)
            disposables.Add(container.GetRequiredService<TierFive>().Disposable);

        for (int i = 0; i < Iterations; i++)
            _ = container.GetRequiredService<TierLeaf>();

        Assert.Equal(Iterations, container.DisposableContainer.DisposableCount);

        container.Dispose();

        Assert.All(disposables, d => Assert.True(d.IsDisposed));
    }

    [Fact]
    public void MissingRequiredConstructorDependency_ThrowsInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierNeedsMissing>();

        for (int i = 0; i < Iterations; i++)
        {
            var exception = Assert.Throws<ServiceTypeNotRegistered>(() => container.GetService<TierNeedsMissing>());
            Assert.Contains(nameof(ITierMissing), exception.Message);
        }
    }

    [Fact]
    public void ConstructorException_PropagatesUnwrappedInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierThrowingConstructor>();

        for (int i = 0; i < Iterations; i++)
            Assert.Throws<TierConstructorException>(() => container.GetService<TierThrowingConstructor>());
    }

    [Fact]
    public void PropertyInjection_BehavesTheSameInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterSingleton<TierLeaf>(new TierLeaf(), serviceKey: "k");
        container.RegisterTransient<TierWithProperties>();

        var keyed = container.GetRequiredKeyedService<TierLeaf>("k");

        for (int i = 0; i < Iterations; i++)
        {
            var instance = container.GetRequiredService<TierWithProperties>();

            Assert.NotNull(instance.Required);
            Assert.NotSame(keyed, instance.Required);
            Assert.Same(keyed, instance.Keyed);
            Assert.Null(instance.OptionalMissing);
            Assert.Equal(0, instance.OptionalValue);
            Assert.Null(instance.NotInjected);
        }
    }

    [Fact]
    public void RequiredPropertyMissing_ConstructsThenThrowsInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierRequiredPropertyMissing>();

        int before = Volatile.Read(ref TierRequiredPropertyMissing.Constructed);

        for (int i = 0; i < Iterations; i++)
            Assert.Throws<ServiceTypeNotRegistered>(() => container.GetService<TierRequiredPropertyMissing>());

        Assert.Equal(Iterations, Volatile.Read(ref TierRequiredPropertyMissing.Constructed) - before);
    }

    [Fact]
    public void KeyedConstructorParameter_ResolvesTheKeyedServiceInEveryTier()
    {
        var container = new ServiceContainer();
        var keyed = new TierLeaf();
        container.RegisterSingleton<TierLeaf>(keyed, serviceKey: "k");
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierKeyedConsumer>();

        for (int i = 0; i < Iterations; i++)
        {
            var consumer = container.GetRequiredService<TierKeyedConsumer>();

            Assert.Same(keyed, consumer.Keyed);
            Assert.NotSame(keyed, consumer.Unkeyed);
        }
    }

    [Fact]
    public void FrozenContainer_BehavesTheSameInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterTransient<TierDisposableLeaf>();
        container.RegisterTransient<TierFive>();
        container.RegisterTransient<TierNine>();
        container.RegisterScoped<TierWithDefaults>();
        container.Freeze();

        for (int i = 0; i < Iterations; i++)
        {
            Assert.Equal(9, container.GetRequiredService<TierNine>().Leaves.Distinct().Count());
            Assert.NotNull(container.GetRequiredService<TierFive>().Disposable);

            using var scope = container.CreateScope();
            var first = scope.ServiceProvider.GetRequiredService<TierWithDefaults>();
            Assert.Same(first, scope.ServiceProvider.GetRequiredService<TierWithDefaults>());
            Assert.Equal(42, first.Number);
        }

        Assert.Equal(Iterations, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void CreateInstance_WithDependenciesAndProperties_BehavesTheSameInEveryTier()
    {
        var container = new ServiceContainer();
        container.RegisterTransient<TierLeaf>();
        container.RegisterSingleton<TierLeaf>(new TierLeaf(), serviceKey: "k");

        for (int i = 0; i < Iterations; i++)
        {
            var withDefaults = container.CreateInstance<TierWithDefaults>();
            Assert.Equal(42, withDefaults.Number);
            Assert.Equal(TierMode.Second, withDefaults.Mode);
            Assert.Equal(TierMode.First, withDefaults.OptionalMode);

            var withProperties = container.CreateInstance<TierWithProperties>();
            Assert.NotNull(withProperties.Required);
            Assert.NotNull(withProperties.Keyed);

            Assert.Equal(9, container.CreateInstance<TierNine>().Leaves.Distinct().Count());
        }
    }

    [Fact]
    public void CreateInstance_ConstructorException_PropagatesUnwrappedInEveryTier()
    {
        var container = new ServiceContainer();

        for (int i = 0; i < Iterations; i++)
            Assert.Throws<TierConstructorException>(() => container.CreateInstance<TierThrowingConstructor>());
    }

    [Fact]
    public void Scoped_MoreThanEightServicesInOneScope_KeepsOneInstancePerScope()
    {
        var types = new[]
        {
            typeof(TierScoped0), typeof(TierScoped1), typeof(TierScoped2), typeof(TierScoped3),
            typeof(TierScoped4), typeof(TierScoped5), typeof(TierScoped6), typeof(TierScoped7),
            typeof(TierScoped8), typeof(TierScoped9), typeof(TierScoped10), typeof(TierScoped11)
        };

        var container = new ServiceContainer();
        foreach (var type in types)
            container.Register(Abstractions.Implementation.ServiceDescriptor.Scoped(type, type));

        using var scope = container.CreateScope();
        var first = new Dictionary<Type, object>();

        foreach (var type in types)
        {
            var instance = scope.ServiceProvider.GetRequiredService(type);
            first[type] = instance;

            // Every earlier instance is still found after each insertion, including across the move to the dictionary.
            foreach (var seen in first)
                Assert.Same(seen.Value, scope.ServiceProvider.GetRequiredService(seen.Key));
        }

        using var otherScope = container.CreateScope();
        foreach (var type in types)
            Assert.NotSame(first[type], otherScope.ServiceProvider.GetRequiredService(type));
    }

    [Fact]
    public async Task Scoped_ConcurrentResolutionAcrossTheMoveToTheDictionary_ReturnsOneInstancePerType()
    {
        var types = new[]
        {
            typeof(TierScoped0), typeof(TierScoped1), typeof(TierScoped2), typeof(TierScoped3),
            typeof(TierScoped4), typeof(TierScoped5), typeof(TierScoped6), typeof(TierScoped7),
            typeof(TierScoped8), typeof(TierScoped9), typeof(TierScoped10), typeof(TierScoped11)
        };

        var container = new ServiceContainer();
        foreach (var type in types)
            container.Register(Abstractions.Implementation.ServiceDescriptor.Scoped(type, type));

        for (int round = 0; round < 20; round++)
        {
            using var scope = container.CreateScope();
            var provider = scope.ServiceProvider;

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                var resolved = new object[types.Length];
                for (int i = 0; i < types.Length; i++)
                {
                    int index = (i + worker) % types.Length;
                    resolved[index] = provider.GetRequiredService(types[index]);
                }

                return resolved;
            })));

            for (int i = 0; i < types.Length; i++)
            {
                var expected = provider.GetRequiredService(types[i]);
                Assert.All(results, r => Assert.Same(expected, r[i]));
            }
        }
    }
}
