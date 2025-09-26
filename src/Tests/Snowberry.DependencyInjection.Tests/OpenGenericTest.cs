using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Tests.Services;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests;

public class OpenGenericTest
{
    [Fact]
    public void TestRegisterAndInstantiation_Singleton()
    {
        var serviceContainer = new ServiceContainer();

        serviceContainer.Register(typeof(IOpenGenericService<>), typeof(OpenGenericService<>), serviceKey: null, lifetime: ServiceLifetime.Singleton, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: null, lifetime: ServiceLifetime.Singleton, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: "TestKey", lifetime: ServiceLifetime.Scoped, singletonInstance: null);

        var service = serviceContainer.GetService<IOpenGenericService<OpenGenericTest>>();
        Assert.Equal(typeof(OpenGenericTest).FullName, service.ToStringT());
        Assert.Same(service, serviceContainer.GetService<IOpenGenericService<OpenGenericTest>>());
        Assert.NotSame(service.TestProperty, service.TestPropertyKeyed);
    }

    [Fact]
    public void TestRegisterAndInstantiation_Scoped()
    {
        var serviceContainer = new ServiceContainer();

        serviceContainer.Register(typeof(IOpenGenericService<>), typeof(OpenGenericService<>), serviceKey: null, lifetime: ServiceLifetime.Scoped, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: null, lifetime: ServiceLifetime.Scoped, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: "TestKey", lifetime: ServiceLifetime.Scoped, singletonInstance: null);

        var service = serviceContainer.GetService<IOpenGenericService<OpenGenericTest>>();
        Assert.Equal(typeof(OpenGenericTest).FullName, service.ToStringT());

        using var scope = serviceContainer.CreateScope();
        var scopedService = scope.ServiceFactory.GetService<IOpenGenericService<OpenGenericTest>>();
        Assert.NotSame(service, scopedService);
        Assert.Same(scopedService, scope.ServiceFactory.GetService<IOpenGenericService<OpenGenericTest>>());
        Assert.Same(scopedService.TestProperty, scope.ServiceFactory.GetService<IOpenGenericService2<float>>());
        Assert.NotSame(scopedService.TestProperty, service.TestProperty);
        Assert.NotSame(scopedService.TestProperty, scopedService.TestPropertyKeyed);
    }

    [Fact]
    public void TestRegisterAndInstantiation_Transient()
    {
        var serviceContainer = new ServiceContainer();

        serviceContainer.Register(typeof(IOpenGenericService<>), typeof(OpenGenericService<>), serviceKey: null, lifetime: ServiceLifetime.Transient, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: null, lifetime: ServiceLifetime.Transient, singletonInstance: null);
        serviceContainer.Register(typeof(IOpenGenericService2<>), typeof(OpenGenericService2<>), serviceKey: "TestKey", lifetime: ServiceLifetime.Singleton, singletonInstance: null);

        var service1 = serviceContainer.GetService<IOpenGenericService<OpenGenericTest>>();
        var service2 = serviceContainer.GetService<IOpenGenericService<OpenGenericTest>>();
        Assert.Equal(typeof(OpenGenericTest).FullName, service1.ToStringT());

        Assert.NotSame(service1, service2);
        Assert.NotSame(service1.TestProperty, service2.TestProperty);
        Assert.Same(service1.TestPropertyKeyed, service2.TestPropertyKeyed);
    }
}
