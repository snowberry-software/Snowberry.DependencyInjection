namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Basic interface for testing dependency injection functionality.
/// </summary>
public interface ITestService : IDisposable
{
    string Name { get; set; }
    bool IsDisposed { get; }
    void DoWork();
}

/// <summary>
/// Interface for testing services with dependencies.
/// </summary>
public interface IDependentService : IDisposable
{
    ITestService PrimaryDependency { get; }
    ITestService? OptionalDependency { get; }
    bool IsDisposed { get; }
    string GetDependencyInfo();
}

/// <summary>
/// Interface for testing complex dependency scenarios.
/// </summary>
public interface IComplexService : IDisposable
{
    ITestService TestService { get; }
    IDependentService DependentService { get; }
    string ProcessData(string input);
    bool IsDisposed { get; }
}

/// <summary>
/// Interface for testing keyed services.
/// </summary>
public interface IKeyedService : IDisposable
{
    string ServiceKey { get; }
    string ProcessRequest(string request);
    bool IsDisposed { get; }
}

/// <summary>
/// Interface for testing repository pattern.
/// </summary>
public interface IRepository<T> : IDisposable
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    void Add(T entity);
    bool IsDisposed { get; }
}

/// <summary>
/// Interface for testing open generic services.
/// </summary>
public interface IGenericProcessor<T> : IDisposable
{
    T Process(T input);
    string GetProcessorInfo();
    bool IsDisposed { get; }
}

/// <summary>
/// Interface for testing async disposable services.
/// </summary>
#if NETCOREAPP
public interface IAsyncTestService : IAsyncDisposable
{
    string Name { get; set; }
    bool IsDisposed { get; }
    Task<string> ProcessAsync(string input);
}
#endif

/// <summary>
/// Interface for services with constructor and property injection.
/// </summary>
public interface IHybridService : IDisposable
{
    ITestService ConstructorInjected { get; }
    ITestService? PropertyInjected { get; set; }
    bool IsDisposed { get; }
}