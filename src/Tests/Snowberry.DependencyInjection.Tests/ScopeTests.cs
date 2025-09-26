using Snowberry.DependencyInjection.Abstractions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Tests.Services;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;
using Xunit;

namespace Snowberry.DependencyInjection.Tests;

public class ScopeTests
{
    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void ScopeDispose(ServiceLifetime serviceLifetime)
    {
        using var serviceContainer = new ServiceContainer();
        serviceContainer.Register(typeof(ITestService), typeof(TestService), null, serviceLifetime, null);

        ITestService service;
        using (var scope = serviceContainer.CreateScope())
        {
            service = scope.ServiceFactory.GetService<ITestService>();

            Assert.Equal(1, scope.DisposableCount);

            if (serviceLifetime == ServiceLifetime.Scoped)
            {
                Assert.Equal(service, scope.ServiceFactory.GetService<ITestService>());
                Assert.Equal(service, scope.ServiceFactory.GetOptionalService<ITestService>());
            }
            else
            {
                Assert.NotEqual(service, scope.ServiceFactory.GetService<ITestService>());
                Assert.NotEqual(service, scope.ServiceFactory.GetOptionalService<ITestService>());
            }
        }

        Assert.Equal(0, serviceContainer.DisposableCount);
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public void GlobalScope()
    {
        ITestService globalTestService;
        ITestService scopedTestService;

        IServiceContainer serviceContainer;
        IScope scope;
        using (serviceContainer = new ServiceContainer())
        {
            serviceContainer.RegisterScoped<ITestService, TestService>();

            globalTestService = serviceContainer.GetService<ITestService>();

            using (scope = serviceContainer.CreateScope())
            {
                scopedTestService = scope.ServiceFactory.GetService<ITestService>();

                Assert.Equal(1, scope.DisposableCount);
                Assert.Equal(scopedTestService, scope.ServiceFactory.GetService<ITestService>());
            }

            Assert.NotEqual(globalTestService, scopedTestService);
            Assert.True(scopedTestService.IsDisposed);

            Assert.Equal(1, scope.DisposableCount);
            Assert.Equal(1, serviceContainer.DisposableCount);
        }

        Assert.Equal(1, serviceContainer.DisposableCount);
        Assert.Equal(1, scope.DisposableCount);
        Assert.True(scope.IsDisposed);
        Assert.True(serviceContainer.IsDisposed);
    }

    [Fact]
    public void GlobalScopeWithFactory()
    {
        ITestService globalTestService;
        ITestService scopedTestService;

        IServiceContainer serviceContainer;
        IScope scope;
        using (serviceContainer = new ServiceContainer())
        {
            serviceContainer.RegisterScoped<ITestService, TestService>(instanceFactory: (sp, serviceKey) =>
            {
                return new TestService
                {
                    Name = "Factory1337"
                };
            });

            globalTestService = serviceContainer.GetService<ITestService>();

            Assert.Equal("Factory1337", globalTestService.Name);

            using (scope = serviceContainer.CreateScope())
            {
                scopedTestService = scope.ServiceFactory.GetService<ITestService>();

                Assert.Equal(1, scope.DisposableCount);
                Assert.Equal(scopedTestService, scope.ServiceFactory.GetService<ITestService>());
                Assert.Equal("Factory1337", scopedTestService.Name);
            }

            Assert.NotEqual(globalTestService, scopedTestService);
            Assert.True(scopedTestService.IsDisposed);

            Assert.Equal(1, scope.DisposableCount);
            Assert.Equal(1, serviceContainer.DisposableCount);
        }

        Assert.Equal(1, serviceContainer.DisposableCount);
        Assert.Equal(1, scope.DisposableCount);
        Assert.True(scope.IsDisposed);
        Assert.True(serviceContainer.IsDisposed);
    }
}
