namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Shared, ordered record of disposal calls made by the disposal test services.
/// </summary>
public sealed class DisposalLog
{
    private readonly List<string> _entries = [];

    /// <summary>
    /// Records that the service named <paramref name="name"/> was disposed.
    /// </summary>
    /// <param name="name">The name of the disposed service.</param>
    public void Add(string name)
    {
        lock (_entries)
            _entries.Add(name);
    }

    /// <summary>
    /// Gets a snapshot of the recorded names, in disposal order.
    /// </summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_entries)
                return [.. _entries];
        }
    }
}

/// <summary>
/// Synchronously disposable service that records its disposal and optionally throws afterwards.
/// </summary>
/// <param name="name">The name recorded in <paramref name="log"/> on disposal.</param>
/// <param name="log">The log that records the disposal.</param>
/// <param name="failure">The exception thrown after recording the disposal, or <see langword="null"/> to dispose successfully.</param>
public sealed class RecordingDisposable(string name, DisposalLog log, Exception? failure = null) : IDisposable
{
    /// <summary>
    /// Gets the name recorded on disposal.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets a value indicating whether <see cref="Dispose"/> has been called.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        IsDisposed = true;
        log.Add(Name);

        if (failure != null)
            throw failure;
    }
}

#if NETCOREAPP
/// <summary>
/// Asynchronously disposable service that records its disposal and optionally fails, either synchronously
/// (before returning the <see cref="ValueTask"/>) or after yielding.
/// </summary>
/// <param name="name">The name recorded in <paramref name="log"/> on disposal.</param>
/// <param name="log">The log that records the disposal.</param>
/// <param name="failure">The exception the disposal fails with, or <see langword="null"/> to dispose successfully.</param>
/// <param name="failAfterYield"><see langword="true"/> to fail through the returned <see cref="ValueTask"/> after yielding; <see langword="false"/> to throw before returning it.</param>
public sealed class RecordingAsyncDisposable(string name, DisposalLog log, Exception? failure = null, bool failAfterYield = false) : IAsyncDisposable
{
    /// <summary>
    /// Gets the name recorded on disposal.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets a value indicating whether <see cref="DisposeAsync"/> has been called.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        log.Add(Name);

        if (failure == null)
            return default;

        if (!failAfterYield)
            throw failure;

        return FailAfterYieldAsync(failure);
    }

    /// <summary>
    /// Yields, then throws <paramref name="failure"/> through the returned task.
    /// </summary>
    /// <param name="failure">The exception to throw.</param>
    /// <returns>A task that faults with <paramref name="failure"/>.</returns>
    private static async ValueTask FailAfterYieldAsync(Exception failure)
    {
        await Task.Yield();
        throw failure;
    }
}
#endif
