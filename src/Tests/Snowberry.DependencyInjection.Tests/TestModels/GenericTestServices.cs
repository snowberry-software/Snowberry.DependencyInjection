namespace Snowberry.DependencyInjection.Tests.TestModels;

/// <summary>
/// Generic repository implementation for testing open generic types.
/// </summary>
public class Repository<T> : IRepository<T>
{
    private readonly List<T> _entities = [];
    public bool IsDisposed { get; private set; }

    public T? GetById(int id)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(Repository<T>));

        return _entities.ElementAtOrDefault(id);
    }

    public IEnumerable<T> GetAll()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(Repository<T>));

        return _entities.AsReadOnly();
    }

    public void Add(T entity)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(Repository<T>));

        _entities.Add(entity);
    }

    public void Dispose()
    {
        IsDisposed = true;
        _entities.Clear();
    }
}

/// <summary>
/// Specific repository implementation for TestEntity.
/// </summary>
public class TestEntityRepository : IRepository<TestEntity>
{
    private readonly List<TestEntity> _entities = [];
    public bool IsDisposed { get; private set; }

    public TestEntity? GetById(int id)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(TestEntityRepository));

        return _entities.FirstOrDefault(e => e.Id == id);
    }

    public IEnumerable<TestEntity> GetAll()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(TestEntityRepository));

        return _entities.AsReadOnly();
    }

    public void Add(TestEntity entity)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(TestEntityRepository));

        _entities.Add(entity);
    }

    public void Dispose()
    {
        IsDisposed = true;
        _entities.Clear();
    }
}

/// <summary>
/// Generic processor for testing open generic services.
/// </summary>
public class GenericProcessor<T> : IGenericProcessor<T>
{
    public bool IsDisposed { get; private set; }

    public T Process(T input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GenericProcessor<T>));

        return input; // Simple pass-through for testing
    }

    public string GetProcessorInfo()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(GenericProcessor<T>));

        return $"GenericProcessor<{typeof(T).Name}>";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Specific string processor implementation.
/// </summary>
public class StringProcessor : IGenericProcessor<string>
{
    public bool IsDisposed { get; private set; }

    public string Process(string input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(StringProcessor));

        return $"Processed: {input}";
    }

    public string GetProcessorInfo()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(StringProcessor));

        return "StringProcessor";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Alternative generic processor implementation.
/// </summary>
public class AlternativeProcessor<T> : IGenericProcessor<T>
{
    public bool IsDisposed { get; private set; }

    public T Process(T input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AlternativeProcessor<T>));

        return input;
    }

    public string GetProcessorInfo()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(AlternativeProcessor<T>));

        return $"AlternativeProcessor<{typeof(T).Name}>";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Complex generic service with dependencies for testing.
/// </summary>
public class ComplexGenericService<T> : IGenericProcessor<T>
{
    private readonly IRepository<T> _repository;
    public bool IsDisposed { get; private set; }

    public ComplexGenericService(IRepository<T> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public T Process(T input)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ComplexGenericService<T>));

        _repository.Add(input);
        return input;
    }

    public string GetProcessorInfo()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ComplexGenericService<T>));

        return $"ComplexGenericService<{typeof(T).Name}> with Repository";
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}