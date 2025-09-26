using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Tests.Services;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;
using Snowberry.DependencyInjection.Tests.Services.PropertyTestServices;
using Xunit;

namespace Snowberry.DependencyInjection.Tests;

public class DefaultTests
{
    [Fact]
    public void OverwriteExistingTypeDescriptor()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestService>(new TestService()
        {
            Name = "1"
        });

        Assert.Equal("1", serviceContainer.GetService<ITestService>().Name);

        serviceContainer.RegisterSingleton<ITestService>(new TestService()
        {
            Name = "2"
        });

        Assert.Equal(0, serviceContainer.DisposableCount);
        Assert.Equal("2", serviceContainer.GetService<ITestService>().Name);
        Assert.Equal(1, serviceContainer.Count);
    }

    [Fact]
    public void CantOverwriteExistingTypeDescriptor()
    {
        using var serviceContainer = new ServiceContainer(ServiceContainerOptions.ReadOnly);
        serviceContainer.RegisterSingleton<ITestService, TestService>();

        Assert.Throws<ServiceRegistryReadOnlyException>(() =>
        {
            serviceContainer.RegisterSingleton<ITestService, TestService>();
        });
    }

    [Fact]
    public void InvalidServiceImplementationType()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestService>();

        Assert.Throws<InvalidServiceImplementationType>(serviceContainer.GetService<ITestService>);
    }

    [Fact]
    public void OptionalServiceNotRegistered()
    {
        using var serviceContainer = new ServiceContainer();

        Assert.Throws<ServiceTypeNotRegistered>(serviceContainer.GetService<ITestService>);
        Assert.Null(serviceContainer.GetOptionalService<ITestService>());
    }

    [Fact]
    public void OptionalServiceRegistered()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestService, TestService>();

        Assert.NotNull(serviceContainer.GetOptionalService<ITestService>());
        Assert.Equal(1, serviceContainer.DisposableCount);
    }

    [Fact]
    public void ConstructorAndPropertyInjection()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestService, TestService>();
        serviceContainer.RegisterSingleton<ITestAnotherService, TestAnotherServiceFull>();

        var anotherService = serviceContainer.GetService<ITestAnotherService>();
        Assert.NotNull(anotherService);

        Assert.NotNull(anotherService.TestService);
        Assert.NotNull(anotherService.TestServiceSame);

        Assert.Equal(anotherService.TestService, anotherService.TestServiceSame);

        Assert.Equal(1, serviceContainer.DisposableCount);
    }

    [Fact]
    public void ConstructorInjectionServiceIsMissing()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestAnotherService, TestAnotherServiceFull>();

        Assert.Throws<ServiceTypeNotRegistered>(serviceContainer.GetService<ITestAnotherService>);

        Assert.Equal(0, serviceContainer.DisposableCount);
    }

    [Fact]
    public void PropertyInjectionRequiredButMissing()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestAnotherService, TestAnotherServicePropertyRequired>();

        Assert.Throws<ServiceTypeNotRegistered>(serviceContainer.GetService<ITestAnotherService>);

        Assert.Equal(0, serviceContainer.DisposableCount);
    }

    [Fact]
    public void PropertyInjectionServiceNotRequiredAndMissing()
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.RegisterSingleton<ITestAnotherService, TestAnotherServicePropertyNotRequired>();

        var service = serviceContainer.GetService<ITestAnotherService>();
        Assert.Null(service.TestService);

        Assert.Equal(0, serviceContainer.DisposableCount);
    }

    [Fact]
    public void DisposableMustBeDisposed()
    {
        ITestService disposable;
        using (var serviceContainer = new ServiceContainer())
        {
            serviceContainer.RegisterSingleton<ITestService, TestService>();

            disposable = serviceContainer.GetService<ITestService>();

            Assert.Equal(1, serviceContainer.DisposableCount);
        }

        Assert.True(disposable.IsDisposed);
    }

    [Fact]
    public void DisposableThatCantBeDisposed()
    {
        // The user provided the instance which means that the instance will not be disposed by the service container.

        ITestService disposable = new TestService();
        using (var serviceContainer = new ServiceContainer())
        {
            serviceContainer.RegisterSingleton<ITestService, TestService>((TestService)disposable);

            Assert.Equal(0, serviceContainer.DisposableCount);

            disposable = serviceContainer.GetService<ITestService>();
        }

        Assert.False(disposable.IsDisposed);
    }

    [Fact]
    public void ContainerAlreadyDisposed()
    {
        ServiceContainer serviceContainer;
        using (serviceContainer = new ServiceContainer())
        {
        }

        Assert.Throws<ObjectDisposedException>(() => serviceContainer.Register(typeof(ITestService), typeof(TestService), null, ServiceLifetime.Singleton, null));
        Assert.Throws<ObjectDisposedException>(serviceContainer.CreateScope);
        Assert.Throws<ObjectDisposedException>(serviceContainer.GetService<ITestService>);
    }

    [Fact]
    public void UnregisterService()
    {
        var serviceContainer = new ServiceContainer();

        TestService instance;
        serviceContainer.RegisterSingleton<ITestService, TestService>(instance = new TestService());

        Assert.True(serviceContainer.IsServiceRegistered<ITestService>(null));
        Assert.False(instance.IsDisposed);

        serviceContainer.UnregisterService<ITestService>(null, out bool successful);

        Assert.True(successful);
        Assert.False(serviceContainer.IsServiceRegistered<ITestService>(null));
        Assert.True(instance.IsDisposed);

    }
}
