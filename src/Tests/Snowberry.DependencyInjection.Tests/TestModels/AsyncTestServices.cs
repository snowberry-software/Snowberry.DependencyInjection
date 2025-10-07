namespace Snowberry.DependencyInjection.Tests.TestModels;

#if NETCOREAPP
/// <summary>
/// Async disposable test service implementation.
/// </summary>
public class AsyncTestService : IAsyncTestService
{
    public string Name { get; set; } = "AsyncTestService";
    public bool IsDisposed { get; private set; }

    public async Task<string> ProcessAsync(string input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AsyncTestService));

        await Task.Delay(10); // Simulate async work
        return $"Async processed: {input}";
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            //await Task.Delay(50); // Simulate async cleanup
            IsDisposed = true;
        }
    }
}

/// <summary>
/// Service that implements both sync and async disposal.
/// </summary>
public class HybridDisposableService : ITestService, IAsyncDisposable
{
    public string Name { get; set; } = "HybridDisposableService";
    public bool IsDisposed { get; private set; }

    public void DoWork()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(HybridDisposableService));
    }

    public void Dispose()
    {
        // This should not be called when IAsyncDisposable is preferred
        throw new InvalidOperationException("Sync Dispose should not be called when async is available");
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            await Task.Delay(25); // Simulate async cleanup
            IsDisposed = true;
        }
    }
}

/// <summary>
/// Async service with dependencies for testing complex scenarios.
/// </summary>
public class AsyncDependentService : IAsyncTestService
{
    private readonly ITestService _dependency;
    public string Name { get; set; } = "AsyncDependentService";
    public bool IsDisposed { get; private set; }

    public AsyncDependentService(ITestService dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }

    public async Task<string> ProcessAsync(string input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AsyncDependentService));

        await Task.Delay(15); // Simulate async work
        return $"Async processed with dependency {_dependency.Name}: {input}";
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            await Task.Delay(30); // Simulate async cleanup
            IsDisposed = true;
        }
    }
}
#endif