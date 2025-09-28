using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Tests.TestModels;
using Xunit;

namespace Snowberry.DependencyInjection.Tests.Core;

/// <summary>
/// Tests for transient service lifetime behavior including instance creation,
/// factory methods, and disposal patterns.
/// </summary>
public class TransientLifetimeTests
{
    [Fact]
    public void GetService_WithTransientRegistration_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.IsType<TestService>(service2);
        Assert.Equal(2, container.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientConcreteType_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<TestService>();

        // Act
        var service1 = container.GetRequiredService<TestService>();
        var service2 = container.GetRequiredService<TestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.IsType<TestService>(service1);
        Assert.IsType<TestService>(service2);
        Assert.Equal(2, container.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientFactory_ShouldCallFactoryEachTime()
    {
        // Arrange
        using var container = new ServiceContainer();
        int callCount = 0;
        container.RegisterTransient<ITestService>((sp, key) =>
        {
            callCount++;
            return new TestService { Name = $"Instance_{callCount}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.Equal("Instance_1", service1.Name);
        Assert.Equal("Instance_2", service2.Name);
        Assert.Equal(2, callCount);
        Assert.Equal(2, container.DisposableCount);
    }

    [Fact]
    public void GetService_WithTransientAndModification_ShouldNotAffectOtherInstances()
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();
        service1.Name = "Modified";

        // Assert
        Assert.Equal("Modified", service1.Name);
        Assert.Equal("DefaultTestService", service2.Name);
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void DisposeContainer_WithTransientServices_ShouldDisposeAllInstances()
    {
        // Arrange
        TestService service1, service2, service3;
        using (var container = new ServiceContainer())
        {
            container.RegisterTransient<ITestService, TestService>();
            container.RegisterTransient<TestService>();

            service1 = (TestService)container.GetRequiredService<ITestService>();
            service2 = (TestService)container.GetRequiredService<ITestService>();
            service3 = container.GetRequiredService<TestService>();

            Assert.Equal(3, container.DisposableCount);
        }

        // Assert
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
        Assert.True(service3.IsDisposed);
    }

    [Fact]
    public void GetService_WithTransientFactoryParameters_ShouldPassCorrectValues()
    {
        // Arrange
        using var container = new ServiceContainer();
        var capturedKeys = new List<object?>();

        container.RegisterTransient<ITestService>((provider, serviceKey) =>
        {
            capturedKeys.Add(serviceKey);
            return new TestService { Name = $"Call_{capturedKeys.Count}" };
        });

        // Act
        var service1 = container.GetRequiredService<ITestService>();
        var service2 = container.GetRequiredService<ITestService>();

        // Assert
        Assert.Equal(2, capturedKeys.Count);
        Assert.All(capturedKeys, Assert.Null);
        Assert.Equal("Call_1", service1.Name);
        Assert.Equal("Call_2", service2.Name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetService_WithMultipleTransientCalls_ShouldTrackAllInstances(int instanceCount)
    {
        // Arrange
        using var container = new ServiceContainer();
        container.RegisterTransient<ITestService, TestService>();

        // Act
        var services = new List<ITestService>();
        for (int i = 0; i < instanceCount; i++)
        {
            services.Add(container.GetRequiredService<ITestService>());
        }

        // Assert
        Assert.Equal(instanceCount, container.DisposableCount);
        Assert.Equal(instanceCount, services.Count);

        // Verify all instances are unique
        for (int i = 0; i < services.Count; i++)
        {
            for (int j = i + 1; j < services.Count; j++)
            {
                Assert.NotSame(services[i], services[j]);
            }
        }
    }
}