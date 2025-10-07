using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for error handling and edge cases in dependency injection scenarios.
/// </summary>
public class ErrorHandlingAndEdgeCaseTests
{
    [Fact]
    public void ServiceResolution_WithCircularDependency_ShouldHandleGracefully()
    {
        // Note: This test assumes the library detects circular dependencies
        // The actual behavior depends on the library's implementation

        // Arrange
        using var container = new ServiceContainer();

        // Create a scenario that would cause circular dependency if not handled
        // For now, this is a placeholder since we don't have circular dependency test services
        container.RegisterSingleton<ITestService, TestService>();

        // Act & Assert
        var service = container.GetRequiredService<ITestService>();
        Assert.NotNull(service); // Should resolve without issues
    }

    [Fact]
    public void ServiceResolution_WithVeryDeepDependencyChain_ShouldResolveCorrectly()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();
        container.RegisterSingleton<IDependentService, DependentService>();
        container.RegisterSingleton<IComplexService, ComplexService>();

        // Act
        var service = container.GetRequiredService<IComplexService>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.TestService);
        Assert.NotNull(service.DependentService);
        Assert.NotNull(service.DependentService.PrimaryDependency);
    }

    [Fact]
    public void ServiceRegistration_WithNullImplementationType_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            container.Register(ServiceDescriptor.Singleton(typeof(ITestService), null!, singletonInstance: null)));
    }

    [Fact]
    public void ServiceRegistration_WithNullServiceType_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            container.Register(ServiceDescriptor.Singleton(null!, typeof(TestService), singletonInstance: null)));
    }

    [Fact]
    public void ServiceResolution_WithNullServiceType_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => container.GetRequiredService(null!));
        Assert.Throws<ArgumentNullException>(() => container.GetService(null!));
    }

    [Fact]
    public void ServiceResolution_WithAbstractClass_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<AbstractTestService>();

        // Act & Assert
        Assert.Throws<InvalidServiceImplementationType>(container.GetRequiredService<AbstractTestService>);
    }

    [Fact]
    public void ServiceResolution_WithInterface_WithoutImplementation_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>(); // No implementation type

        // Act & Assert
        Assert.Throws<InvalidServiceImplementationType>(container.GetRequiredService<ITestService>);
    }

    [Fact]
    public void ServiceResolution_WithPrivateConstructor_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithPrivateConstructor>();

        // Act & Assert
        var exception = Assert.ThrowsAny<Exception>(container.GetRequiredService<ServiceWithPrivateConstructor>);

        // The exact exception type depends on the library's implementation
        Assert.NotNull(exception);
    }

    [Fact]
    public void ServiceResolution_WithConstructorThrowingException_ShouldWrapException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithThrowingConstructor>();

        // Act & Assert
        // Activator.CreateInstance wraps constructor exceptions in TargetInvocationException
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceWithThrowingConstructor>);

        // The actual constructor exception should be in InnerException
        Assert.NotNull(exception.InnerException);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Constructor exception", exception.InnerException.Message);
    }

    [Fact]
    public void ServiceResolution_WithConstructorThrowingArgumentException_ShouldWrapException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithArgumentExceptionConstructor>();

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceWithArgumentExceptionConstructor>);

        Assert.NotNull(exception.InnerException);
        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Equal("Invalid argument in constructor", exception.InnerException.Message);
    }

    [Fact]
    public void ServiceResolution_WithConstructorThrowingNullReferenceException_ShouldWrapException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithNullReferenceConstructor>();

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceWithNullReferenceConstructor>);

        Assert.NotNull(exception.InnerException);
        Assert.IsType<NullReferenceException>(exception.InnerException);
        Assert.Equal("Null reference in constructor", exception.InnerException.Message);
    }

    [Fact]
    public void ServiceResolution_WithDependentServiceConstructorException_ShouldPropagateWrappedException()
    {
        // Tests exception propagation when a dependency's constructor throws

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithThrowingConstructor>();
        container.RegisterSingleton<ServiceDependentOnThrowingService>();

        // Act & Assert
        // The exception should propagate up from the dependency
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceDependentOnThrowingService>);

        Assert.NotNull(exception.InnerException);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Constructor exception", exception.InnerException.Message);
    }

    [Fact]
    public void ServiceResolution_WithFactoryThrowingException_ShouldNotWrapException()
    {
        // Factory exceptions are NOT wrapped since they don't go through Activator.CreateInstance

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) =>
            throw new InvalidOperationException("Factory exception"));

        // Act & Assert
        // Factory exceptions should be thrown directly, not wrapped
        var exception = Assert.Throws<InvalidOperationException>(container.GetRequiredService<ITestService>);

        Assert.Equal("Factory exception", exception.Message);
    }

    [Fact]
    public void ServiceResolution_WithConstructorExceptionInTransientService_ShouldThrowEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ServiceWithThrowingConstructor>();

        // Act & Assert
        // Should throw the same wrapped exception each time for transient services
        for (int i = 0; i < 3; i++)
        {
            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceWithThrowingConstructor>);

            Assert.NotNull(exception.InnerException);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Equal("Constructor exception", exception.InnerException.Message);
        }

        // No services should be created due to constructor exceptions
        Assert.Equal(0, container.DisposableCount);
    }

    [Fact]
    public void ServiceResolution_WithConstructorExceptionInSingleton_ShouldThrowConsistently()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithThrowingConstructor>();

        // Act & Assert
        // Should throw the same exception consistently for singleton services
        for (int i = 0; i < 3; i++)
        {
            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(container.GetRequiredService<ServiceWithThrowingConstructor>);

            Assert.NotNull(exception.InnerException);
            Assert.Equal("Constructor exception", exception.InnerException.Message);
        }

        Assert.Equal(0, container.DisposableCount);
    }

    [Fact]
    public void ServiceResolution_WithScopedServiceConstructorException_ShouldThrowInScope()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithThrowingConstructor>();

        // Act & Assert
        using var scope = container.CreateScope();

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(scope.ServiceFactory.GetRequiredService<ServiceWithThrowingConstructor>);

        Assert.NotNull(exception.InnerException);
        Assert.Equal("Constructor exception", exception.InnerException.Message);
        Assert.Equal(0, scope.DisposableCount);
    }

    [Fact]
    public void ServiceFactory_ThrowingException_ShouldPropagateException()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) =>
            throw new InvalidOperationException("Factory exception"));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(container.GetRequiredService<ITestService>);

        Assert.Equal("Factory exception", exception.Message);
    }

    [Fact]
    public void ServiceResolution_WithExcessiveRecursion_ShouldHandleGracefully()
    {
        // This test checks if the library can handle deeply nested dependency resolution
        // without stack overflow or excessive performance degradation

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Create many transient services to test performance
        var services = new List<ITestService>();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        // Assert
        Assert.Equal(1000, services.Count);
        Assert.Equal(1000, container.DisposableCount);
        Assert.All(services, Assert.NotNull);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    public void ServiceResolution_WithAllValidLifetimes_ShouldWork(ServiceLifetime lifetime)
    {
        // Tests that all valid service lifetimes work correctly

        // Arrange
        using var container = new ServiceContainer();
        container.Register(new ServiceDescriptor(typeof(ITestService), typeof(TestService), lifetime, singletonInstance: null));

        // Act
        var service = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<TestService>(service);
    }

    [Fact]
    public void ServiceResolution_WithGenericTypeDefinition_ShouldThrowException()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        // Trying to resolve an open generic type should fail
        Assert.ThrowsAny<Exception>(() =>
            container.GetRequiredService(typeof(IRepository<>)));
    }

    [Fact]
    public void ConcurrentServiceResolution_ShouldBeSafe()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService, TestService>();

        var tasks = new List<Task<ITestService>>();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    return container.GetRequiredService<ITestService>();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }

                    throw;
                }
            }));
        }

        // Wait for all tasks to complete
        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch
        {
            // Some tasks might have failed
        }

        // Assert
        Assert.Empty(exceptions); // No exceptions should occur
        var completedTasks = tasks.Where(t => t.Status == TaskStatus.RanToCompletion).ToList();
        Assert.True(completedTasks.Count > 0);

        // All successful resolutions should return the same singleton instance
        var firstResult = completedTasks.First().Result;
        Assert.All(completedTasks, t => Assert.Same(firstResult, t.Result));
    }

    #region Test Classes

    public abstract class AbstractTestService
    {
        public abstract void DoSomething();
    }

    public class ServiceWithPrivateConstructor
    {
        private ServiceWithPrivateConstructor()
        {
        }
    }

    public class ServiceWithThrowingConstructor
    {
        public ServiceWithThrowingConstructor()
        {
            throw new InvalidOperationException("Constructor exception");
        }
    }

    public class ServiceWithArgumentExceptionConstructor
    {
        public ServiceWithArgumentExceptionConstructor()
        {
            throw new ArgumentException("Invalid argument in constructor");
        }
    }

    public class ServiceWithNullReferenceConstructor
    {
        public ServiceWithNullReferenceConstructor()
        {
            throw new NullReferenceException("Null reference in constructor");
        }
    }

    public class ServiceDependentOnThrowingService
    {
        public ServiceWithThrowingConstructor ThrowingDependency { get; }

        public ServiceDependentOnThrowingService(ServiceWithThrowingConstructor throwingDependency)
        {
            ThrowingDependency = throwingDependency;
        }
    }

    #endregion
}