using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;

namespace Snowberry.DependencyInjection.Tests.Services.PropertyTestServices;

/// <summary>
/// Used to test property injection only.
/// </summary>
public class TestAnotherServicePropertyRequired : ITestAnotherService
{
    public int Number => 0;

#nullable disable
    [Inject(true)]
    public ITestService TestService { get; set; }

    public ITestService TestServiceSame => TestService;
#nullable enable
}
