using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Tests.Services.Interfaces;

namespace Snowberry.DependencyInjection.Tests.Services;

public class OpenGenericService<T> : IOpenGenericService<T>
{
    public OpenGenericService(IOpenGenericService2<int> testParam)
    {
        testParam.ToStringT();
    }

    /// <inheritdoc/>
    public string ToStringT()
    {
        return typeof(T).FullName ?? string.Empty;
    }

    /// <inheritdoc/>
    [Inject]
    public IOpenGenericService2<float> TestProperty { get; set; } = null!;

    [Inject]
    [FromKeyedServices("TestKey")]
    public IOpenGenericService2<float> TestPropertyKeyed { get; set; } = null!;
}
