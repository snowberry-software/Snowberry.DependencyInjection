using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Implementation;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core.Validation;

/// <summary>
/// Tests for error handling and edge cases in dependency injection scenarios.
/// </summary>
public class ErrorHandlingAndEdgeCaseTests
{
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
    public void ServiceResolution_WithPrivateConstructor_ShouldThrowInvalidConstructor()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithPrivateConstructor>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(container.GetRequiredService<ServiceWithPrivateConstructor>);

        Assert.Contains("has no valid constructors", exception.Message);
        Assert.Contains(typeof(ServiceWithPrivateConstructor).FullName!, exception.Message);
    }

    [Theory]
    [InlineData(typeof(ServiceWithThrowingConstructor), typeof(InvalidOperationException), "Constructor exception")]
    [InlineData(typeof(ServiceWithArgumentExceptionConstructor), typeof(ArgumentException), "Invalid argument in constructor")]
    [InlineData(typeof(ServiceWithNullReferenceConstructor), typeof(NullReferenceException), "Null reference in constructor")]
    public void ServiceResolution_WithConstructorThrowingException_PropagatesUnwrapped(Type serviceType, Type exceptionType, string message)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Singleton(serviceType, serviceType, singletonInstance: null));

        // Act & Assert
        var exception = Assert.Throws(exceptionType, () => container.GetRequiredService(serviceType));

        Assert.Equal(message, exception.Message);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    public void ServiceResolution_WithConstructorException_ThrowsEachTimeAndTracksNothing(ServiceLifetime lifetime)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.Register(new ServiceDescriptor(typeof(ServiceWithThrowingConstructor), typeof(ServiceWithThrowingConstructor), lifetime, singletonInstance: null));
        using var scope = container.CreateScope();

        // Act & Assert
        // The same unwrapped exception surfaces on every attempt for every lifetime.
        for (int i = 0; i < 3; i++)
        {
            var exception = Assert.Throws<InvalidOperationException>(scope.ServiceProvider.GetRequiredService<ServiceWithThrowingConstructor>);

            Assert.Equal("Constructor exception", exception.Message);
        }

        // No services should be tracked for disposal because every construction failed.
        Assert.Equal(0, container.DisposableContainer.DisposableCount);
        Assert.Equal(0, scope.DisposableContainer.DisposableCount);
    }

    [Fact]
    public void ServiceResolution_WithGenericTypeDefinition_ShouldThrowServiceTypeNotRegistered()
    {
        // Arrange
        using var container = new ServiceContainer();

        // Act & Assert
        // Trying to resolve an open generic type should fail
        Assert.Throws<ServiceTypeNotRegistered>(() =>
            container.GetRequiredService(typeof(IRepository<>)));
    }

    [Fact]
    public void ServiceResolution_WithDependentServiceConstructorException_PropagatesUnwrapped()
    {
        // Tests exception propagation when a dependency's constructor throws

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithThrowingConstructor>();
        container.RegisterSingleton<ServiceDependentOnThrowingService>();

        // Act & Assert
        // The exception should propagate up from the dependency
        var exception = Assert.Throws<InvalidOperationException>(container.GetRequiredService<ServiceDependentOnThrowingService>);

        Assert.Equal("Constructor exception", exception.Message);
    }

    [Fact]
    public void ServiceResolution_WithFactoryThrowingException_PropagatesUnwrapped()
    {
        // Factory exceptions are not wrapped, whatever the lifetime.

        // Arrange
        using var container = new ServiceContainer();
        container.RegisterSingleton<ITestService>((sp, key) =>
            throw new InvalidOperationException("Factory exception"));

        // Act & Assert
        // Factory exceptions should be thrown directly, not wrapped
        var exception = Assert.Throws<InvalidOperationException>(container.GetRequiredService<ITestService>);

        Assert.Equal("Factory exception", exception.Message);
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

    [Fact]
    public void GetService_WhenImplementationIsNotTheServiceType_ThrowsInvalidCastNamingBothTypes()
    {
        var container = new ServiceContainer();
        container.Register(ServiceDescriptor.Transient(typeof(ITestService), typeof(TierLeaf)));
        container.Register(ServiceDescriptor.Transient(typeof(ITestService), typeof(TierLeaf)), serviceKey: "k");

        var unkeyed = Assert.Throws<InvalidCastException>(() => container.GetService<ITestService>());
        var keyed = Assert.Throws<InvalidCastException>(() => container.GetKeyedService<ITestService>("k"));

        foreach (var exception in new[] { unkeyed, keyed })
        {
            Assert.Equal($"Service implementation '{typeof(TierLeaf).FullName}' can't be cast to '{typeof(ITestService).FullName}'!", exception.Message);
            Assert.DoesNotContain("RuntimeType", exception.Message);
        }
    }
}
