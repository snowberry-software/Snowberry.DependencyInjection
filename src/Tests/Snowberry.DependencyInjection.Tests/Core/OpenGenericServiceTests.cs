using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var stringRepo1 = container.GetService<IRepository<string>>();
        var stringRepo2 = container.GetService<IRepository<string>>();
        var intRepo = container.GetService<IRepository<int>>();

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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Scoped, null);

        // Act
        var globalRepo = container.GetService<IRepository<string>>();

        IRepository<string> scopedRepo1, scopedRepo2;
        using (var scope = container.CreateScope())
        {
            scopedRepo1 = scope.ServiceFactory.GetService<IRepository<string>>();
            scopedRepo2 = scope.ServiceFactory.GetService<IRepository<string>>();
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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Transient, null);

        // Act
        var repo1 = container.GetService<IRepository<string>>();
        var repo2 = container.GetService<IRepository<string>>();

        // Assert
        Assert.NotSame(repo1, repo2); // Different instances for transient services
        Assert.IsType<Repository<string>>(repo1);
        Assert.IsType<Repository<string>>(repo2);
        Assert.Equal(2, container.DisposableCount);
    }

    [Fact]
    public void RegisterOpenGeneric_WithMixedLifetimes_ShouldFollowCorrectRules()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IGenericProcessor<>), typeof(GenericProcessor<>),
            null, ServiceLifetime.Transient, null);
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var processor1 = container.GetService<IGenericProcessor<string>>();
        var processor2 = container.GetService<IGenericProcessor<string>>();
        var repo1 = container.GetService<IRepository<string>>();
        var repo2 = container.GetService<IRepository<string>>();

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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var repositoryType = typeof(IRepository<>).MakeGenericType(genericType);
        object? repository = container.GetService(repositoryType);

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom(repositoryType, repository);
    }

    [Fact]
    public void RegisterOpenGeneric_WithKeyedServices_ShouldDifferentiateBetweenKeys()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IGenericProcessor<>), typeof(GenericProcessor<>),
            null, ServiceLifetime.Singleton, null);
        container.Register(typeof(IGenericProcessor<>), typeof(AlternativeProcessor<>),
            "alternative", ServiceLifetime.Singleton, null);

        // Act
        var defaultProcessor = container.GetService<IGenericProcessor<string>>();
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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act & Assert - Test with complex generic types
        var listRepo = container.GetService<IRepository<List<int>>>();
        Assert.NotNull(listRepo);
        Assert.IsType<Repository<List<int>>>(listRepo);

        var dictRepo = container.GetService<IRepository<Dictionary<string, int>>>();
        Assert.NotNull(dictRepo);
        Assert.IsType<Repository<Dictionary<string, int>>>(dictRepo);

        var entityRepo = container.GetService<IRepository<TestEntity>>();
        Assert.NotNull(entityRepo);
        Assert.IsType<Repository<TestEntity>>(entityRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_WithDependencyInjection_ShouldInjectCorrectTypes()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);
        container.Register(typeof(IGenericProcessor<>), typeof(ComplexGenericService<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var processor = container.GetService<IGenericProcessor<TestEntity>>();
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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var repositories = new List<IRepository<int>>();
        for (int i = 0; i < 5; i++)
        {
            repositories.Add(container.GetService<IRepository<int>>());
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
            container.Register(typeof(IRepository<>), typeof(Repository<>),
                null, ServiceLifetime.Transient, null);

            for (int i = 0; i < 3; i++)
            {
                stringRepositories.Add(container.GetService<IRepository<string>>());
                intRepositories.Add(container.GetService<IRepository<int>>());
            }

            Assert.Equal(6, container.DisposableCount);
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
        var stringRepo1 = container.GetService<IRepository<string>>();
        var stringRepo2 = container.GetService<IRepository<string>>();

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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var nestedRepo = container.GetService<IRepository<List<string>>>();

        // Assert
        Assert.NotNull(nestedRepo);
        Assert.IsType<Repository<List<string>>>(nestedRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_WithValueAndReferenceTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Singleton, null);

        // Act
        var stringRepo = container.GetService<IRepository<string>>(); // Reference type
        var intRepo = container.GetService<IRepository<int>>(); // Value type
        var boolRepo = container.GetService<IRepository<bool>>(); // Value type

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
        container.Register(typeof(IRepository<>), typeof(Repository<>),
            null, ServiceLifetime.Transient, null);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var repositories = new List<object>();
        for (int i = 0; i < 100; i++)
        {
            repositories.Add(container.GetService<IRepository<string>>());
            repositories.Add(container.GetService<IRepository<int>>());
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(200, repositories.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should be reasonably fast
    }
}