using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;

namespace Snowberry.DependencyInjection.Tests.Services.KeyedTestServices;

public class TestServiceKeyedC : IKeyedTestService
{
    [PreferredConstructor]
    public TestServiceKeyedC([FromKeyedServices("_KEY1_")] ITestService testService)
    {
        KeyedConstructorTestService = testService;
    }

    public ITestService? KeyedConstructorTestService { get; }

    [Inject]
    [FromKeyedServices("_KEY1_")]
    public ITestService? KeyedPropertyInjectTestService { get; set; }
}
