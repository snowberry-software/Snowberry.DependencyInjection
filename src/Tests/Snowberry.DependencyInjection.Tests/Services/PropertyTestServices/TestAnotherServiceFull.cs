using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;

namespace Snowberry.DependencyInjection.Tests.Services.PropertyTestServices;

/// <summary>
/// Used to test constructor and property injection.
/// </summary>
internal class TestAnotherServiceFull : ITestAnotherService
{
    public TestAnotherServiceFull(ITestService testService, bool testPrimitive0, int testPrimitive1)
    {
        TestServiceSame = testService;
    }

#nullable disable
    [Inject]
    public ITestService TestService { get; set; }

    public ITestService TestServiceSame { get; }
#nullable enable

    public int Number { get; }
}
