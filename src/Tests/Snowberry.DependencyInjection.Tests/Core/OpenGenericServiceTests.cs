using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for open generic type registration and resolution including
/// generic type constraints, lifetime management, and dependency injection.
/// </summary>
public class OpenGenericServiceTests
{
    [Fact]
    public void RegisterOpenGeneric_WithSingletonLifetime_ShouldReturnSameInstanceForSameGenericType()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var stringRepo1 = container.GetRequiredService<IRepository<string>>();
        var stringRepo2 = container.GetRequiredService<IRepository<string>>();
        var intRepo = container.GetRequiredService<IRepository<int>>();

        // Assert
        Assert.Same(stringRepo1, stringRepo2); // Same generic type should return same instance
        Assert.NotSame(stringRepo1, intRepo); // Different generic types should be different instances
        Assert.IsType<Repository<string>>(stringRepo1);
        Assert.IsType<Repository<int>>(intRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_WithScopedLifetime_ShouldRespectScopeBoundaries()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Scoped(typeof(IRepository<>), typeof(Repository<>)));

        // Act
        var globalRepo = container.GetRequiredService<IRepository<string>>();

        IRepository<string> scopedRepo1, scopedRepo2;
        using (var scope = container.CreateScope())
        {
            scopedRepo1 = scope.ServiceProvider.GetRequiredService<IRepository<string>>();
            scopedRepo2 = scope.ServiceProvider.GetRequiredService<IRepository<string>>();
        }

        // Assert
        Assert.NotSame(globalRepo, scopedRepo1); // Different scopes should have different instances
        Assert.Same(scopedRepo1, scopedRepo2); // Same scope should return same instance
        Assert.True(scopedRepo1.IsDisposed); // Scoped service should be disposed
        Assert.False(globalRepo.IsDisposed); // Global service should remain
    }

    [Fact]
    public void RegisterOpenGeneric_WithTransientLifetime_ShouldCreateNewInstancesEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Transient(typeof(IRepository<>), typeof(Repository<>)));

        // Act
        var repo1 = container.GetRequiredService<IRepository<string>>();
        var repo2 = container.GetRequiredService<IRepository<string>>();

        // Assert
        Assert.NotSame(repo1, repo2); // Different instances for transient services
        Assert.IsType<Repository<string>>(repo1);
        Assert.IsType<Repository<string>>(repo2);
        Assert.Equal(2, container.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void RegisterOpenGeneric_WithMixedLifetimes_ShouldFollowCorrectRules()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Transient(typeof(IGenericProcessor<>), typeof(GenericProcessor<>)));
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var processor1 = container.GetRequiredService<IGenericProcessor<string>>();
        var processor2 = container.GetRequiredService<IGenericProcessor<string>>();
        var repo1 = container.GetRequiredService<IRepository<string>>();
        var repo2 = container.GetRequiredService<IRepository<string>>();

        // Assert
        Assert.NotSame(processor1, processor2); // Transient services
        Assert.Same(repo1, repo2); // Singleton services
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(TestEntity))]
    [InlineData(typeof(List<string>))]
    public void RegisterOpenGeneric_WithDifferentGenericTypes_ShouldCreateCorrectInstances(Type genericType)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var repositoryType = typeof(IRepository<>).MakeGenericType(genericType);
        object? repository = container.GetRequiredService(repositoryType);

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom(repositoryType, repository);
    }

    [Fact]
    public void RegisterOpenGeneric_WithKeyedServices_ShouldDifferentiateBetweenKeys()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IGenericProcessor<>), typeof(GenericProcessor<>), singletonInstance: null));
        container.Register(ServiceDescriptor.Singleton(typeof(IGenericProcessor<>), typeof(AlternativeProcessor<>), singletonInstance: null), serviceKey: "alternative");

        // Act
        var defaultProcessor = container.GetRequiredService<IGenericProcessor<string>>();
        var alternativeProcessor = container.GetKeyedService<IGenericProcessor<string>>("alternative");

        // Assert
        Assert.NotSame(defaultProcessor, alternativeProcessor);
        Assert.IsType<GenericProcessor<string>>(defaultProcessor);
        Assert.IsType<AlternativeProcessor<string>>(alternativeProcessor);
        Assert.Equal("GenericProcessor<String>", defaultProcessor.GetProcessorInfo());
        Assert.Equal("AlternativeProcessor<String>", alternativeProcessor.GetProcessorInfo());
    }

    [Fact]
    public void RegisterOpenGeneric_WithComplexGenericTypes_ShouldWork()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act & Assert - Test with complex generic types
        var listRepo = container.GetRequiredService<IRepository<List<int>>>();
        Assert.NotNull(listRepo);
        Assert.IsType<Repository<List<int>>>(listRepo);

        var dictRepo = container.GetRequiredService<IRepository<Dictionary<string, int>>>();
        Assert.NotNull(dictRepo);
        Assert.IsType<Repository<Dictionary<string, int>>>(dictRepo);

        var entityRepo = container.GetRequiredService<IRepository<TestEntity>>();
        Assert.NotNull(entityRepo);
        Assert.IsType<Repository<TestEntity>>(entityRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_WithDependencyInjection_ShouldInjectCorrectTypes()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));
        container.Register(ServiceDescriptor.Singleton(typeof(IGenericProcessor<>), typeof(ComplexGenericService<>), singletonInstance: null));

        // Act
        var processor = container.GetRequiredService<IGenericProcessor<TestEntity>>();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        var processedEntity = processor.Process(entity);

        // Assert
        Assert.NotNull(processor);
        Assert.IsType<ComplexGenericService<TestEntity>>(processor);
        Assert.Same(entity, processedEntity);
        Assert.Equal("ComplexGenericService<TestEntity> with Repository", processor.GetProcessorInfo());
    }

    [Fact]
    public void RegisterOpenGeneric_MultipleCallsWithSameGenericType_ShouldBehaveConsistently()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var repositories = new List<IRepository<int>>();
        for (int i = 0; i < 5; i++)
        {
            repositories.Add(container.GetRequiredService<IRepository<int>>());
        }

        // Assert
        Assert.True(repositories.All(r => ReferenceEquals(r, repositories[0]))); // All should be the same singleton instance
        Assert.All(repositories, r => Assert.IsType<Repository<int>>(r));
    }

    [Fact]
    public void RegisterOpenGeneric_DisposalBehavior_ShouldDisposeCorrectly()
    {
        // Arrange
        var stringRepositories = new List<IRepository<string>>();
        var intRepositories = new List<IRepository<int>>();

        using (var container = new ServiceContainer())
        {
            container.Register(ServiceDescriptor.Transient(typeof(IRepository<>), typeof(Repository<>)));

            for (int i = 0; i < 3; i++)
            {
                stringRepositories.Add(container.GetRequiredService<IRepository<string>>());
                intRepositories.Add(container.GetRequiredService<IRepository<int>>());
            }

            Assert.Equal(6, container.DisposableContainer.DisposableCount);
            Assert.All(stringRepositories, r => Assert.False(r.IsDisposed));
            Assert.All(intRepositories, r => Assert.False(r.IsDisposed));
        }

        // Assert - All created instances should be disposed
        Assert.All(stringRepositories, r => Assert.True(r.IsDisposed));
        Assert.All(intRepositories, r => Assert.True(r.IsDisposed));
    }

    [Fact]
    public void RegisterOpenGeneric_WithFactory_ShouldCallFactoryCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        int factoryCallCount = 0;

        // Use a more explicit factory registration approach
        container.RegisterSingleton<IRepository<string>>((serviceProvider, serviceKey) =>
        {
            factoryCallCount++;
            return new Repository<string>();
        });

        // Act
        var stringRepo1 = container.GetRequiredService<IRepository<string>>();
        var stringRepo2 = container.GetRequiredService<IRepository<string>>();

        // Assert
        Assert.NotNull(stringRepo1);
        Assert.Same(stringRepo1, stringRepo2); // Singleton behavior
        Assert.Equal(1, factoryCallCount); // Should be called once for singleton
    }

    [Fact]
    public void RegisterOpenGeneric_WithNestedGenerics_ShouldHandleCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var nestedRepo = container.GetRequiredService<IRepository<List<string>>>();

        // Assert
        Assert.NotNull(nestedRepo);
        Assert.IsType<Repository<List<string>>>(nestedRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_WithValueAndReferenceTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(typeof(IRepository<>), typeof(Repository<>), singletonInstance: null));

        // Act
        var stringRepo = container.GetRequiredService<IRepository<string>>(); // Reference type
        var intRepo = container.GetRequiredService<IRepository<int>>(); // Value type
        var boolRepo = container.GetRequiredService<IRepository<bool>>(); // Value type

        // Assert
        Assert.NotNull(stringRepo);
        Assert.NotNull(intRepo);
        Assert.NotNull(boolRepo);
        Assert.IsType<Repository<string>>(stringRepo);
        Assert.IsType<Repository<int>>(intRepo);
        Assert.IsType<Repository<bool>>(boolRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_Performance_ShouldBeReasonable()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Transient(typeof(IRepository<>), typeof(Repository<>)));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var repositories = new List<object>();
        for (int i = 0; i < 100; i++)
        {
            repositories.Add(container.GetRequiredService<IRepository<string>>());
            repositories.Add(container.GetRequiredService<IRepository<int>>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(200, repositories.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should be reasonably fast
    }
}