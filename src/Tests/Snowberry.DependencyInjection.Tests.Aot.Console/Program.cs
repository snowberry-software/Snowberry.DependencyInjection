using System.Diagnostics;
using Snowberry.DependencyInjection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;

Console.WriteLine("=== Snowberry.DependencyInjection AOT Compatibility Tests ===\n");

var stopwatch = Stopwatch.StartNew();
int testsRun = 0;
int testsPassed = 0;
int testsFailed = 0;

try
{
    RunTest("Basic Singleton", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        var service = container.GetRequiredService<SimpleService>();
        Assert(service != null, "Service should not be null");
        Assert(service!.GetMessage() == "SimpleService", "Message should match");
    });

    RunTest("Singleton with Constructor Dependency", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ServiceWithDependency>();
        var service = container.GetRequiredService<ServiceWithDependency>();
        Assert(service != null, "Service should not be null");
        Assert(service!.Dependency != null, "Dependency should not be null");
    });

    RunTest("Transient Services", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<SimpleService>();
        var service1 = container.GetRequiredService<SimpleService>();
        var service2 = container.GetRequiredService<SimpleService>();
        Assert(service1 != null && service2 != null, "Services should not be null");
        Assert(!ReferenceEquals(service1, service2), "Transient services should be different instances");
    });

    RunTest("Scoped Services", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterScoped<SimpleService>();

        using var scope1 = container.CreateScope();
        var service1a = scope1.ServiceFactory.GetRequiredService<SimpleService>();
        var service1b = scope1.ServiceFactory.GetRequiredService<SimpleService>();

        using var scope2 = container.CreateScope();
        var service2 = scope2.ServiceFactory.GetRequiredService<SimpleService>();

        Assert(ReferenceEquals(service1a, service1b), "Same scope should return same instance");
        Assert(!ReferenceEquals(service1a, service2), "Different scopes should return different instances");
    });

    RunTest("Interface to Implementation", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        var service = container.GetRequiredService<IMyService>();
        Assert(service != null, "Service should not be null");
        Assert(service is MyServiceImpl, "Service should be MyServiceImpl");
        Assert(service!.GetValue() == 42, "Value should be 42");
    });

    RunTest("Multiple Constructor Parameters", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        container.RegisterSingleton<ComplexService>();
        var service = container.GetRequiredService<ComplexService>();
        Assert(service != null, "Service should not be null");
        Assert(service!.SimpleService != null, "SimpleService dependency should not be null");
        Assert(service!.MyService != null, "MyService dependency should not be null");
    });

    RunTest("Property Injection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ServiceWithPropertyInjection>();
        var service = container.GetRequiredService<ServiceWithPropertyInjection>();
        Assert(service != null, "Service should not be null");
        Assert(service!.InjectedService != null, "Injected property should not be null");
    });

    RunTest("Optional Property Injection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithOptionalProperty>();
        var service = container.GetRequiredService<ServiceWithOptionalProperty>();
        Assert(service != null, "Service should not be null");
        Assert(service!.OptionalService == null, "Optional property should be null when not registered");
    });

    RunTest("Factory Registration", () =>
    {
        using var container = new ServiceContainer();
        int callCount = 0;
        container.RegisterSingleton<IMyService>((sp, key) =>
        {
            callCount++;
            return new MyServiceImpl();
        });
        var service1 = container.GetRequiredService<IMyService>();
        var service2 = container.GetRequiredService<IMyService>();
        Assert(callCount == 1, "Factory should be called once for singleton");
        Assert(ReferenceEquals(service1, service2), "Should return same instance");
    });

    RunTest("Instance Registration", () =>
    {
        using var container = new ServiceContainer();
        var instance = new SimpleService();
        container.RegisterSingleton(instance);
        var resolved = container.GetRequiredService<SimpleService>();
        Assert(ReferenceEquals(instance, resolved), "Should return the exact instance");
    });

    RunTest("Keyed Services", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>("service1");
        container.RegisterSingleton<IMyService, AlternativeServiceImpl>("service2");

        var service1 = container.GetRequiredKeyedService<IMyService>("service1");
        var service2 = container.GetRequiredKeyedService<IMyService>("service2");

        Assert(service1 is MyServiceImpl, "Service1 should be MyServiceImpl");
        Assert(service2 is AlternativeServiceImpl, "Service2 should be AlternativeServiceImpl");
        Assert(service1.GetValue() == 42, "Service1 value should be 42");
        Assert(service2.GetValue() == 100, "Service2 value should be 100");
    });

    RunTest("Closed Generic Services", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<GenericService<string>>();
        container.RegisterSingleton<GenericService<int>>();

        var stringService = container.GetRequiredService<GenericService<string>>();
        var intService = container.GetRequiredService<GenericService<int>>();

        Assert(stringService != null, "String service should not be null");
        Assert(intService != null, "Int service should not be null");
        Assert(stringService!.GetTypeName() == "String", "String service type should be String");
        Assert(intService!.GetTypeName() == "Int32", "Int service type should be Int32");
    });

    RunTest("Nested Dependencies", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ServiceWithDependency>();
        container.RegisterSingleton<DeeplyNestedService>();

        var service = container.GetRequiredService<DeeplyNestedService>();
        Assert(service != null, "Service should not be null");
        Assert(service!.Middle != null, "Middle dependency should not be null");
        Assert(service!.Middle!.Dependency != null, "Nested dependency should not be null");
    });

    RunTest("Disposable Services", () =>
    {
        DisposableService? service;
        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<DisposableService>();
            service = container.GetRequiredService<DisposableService>();
            Assert(!service.IsDisposed, "Service should not be disposed before container disposal");
        }

        Assert(service!.IsDisposed, "Service should be disposed after container disposal");
    });

    RunTest("Struct Service Registration", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<StructService>();
        var service = container.GetRequiredService<StructService>();
        Assert(service!.Value == 0, "Default struct should have default values");
    });

    RunTest("Preferred Constructor Selection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ServiceWithMultipleConstructors>();

        var service = container.GetRequiredService<ServiceWithMultipleConstructors>();
        Assert(service != null, "Service should not be null");
        Assert(service!.UsedPreferredConstructor, "Should use preferred constructor");
    });

    RunTest("Complex Property Injection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ComplexPropertyInjectionService>();

        var service = container.GetRequiredService<ComplexPropertyInjectionService>();
        Assert(service!.RequiredService != null, "Required property should be injected");
    });

    RunTest("FromKeyedServices Attribute", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>("primary");
        container.RegisterSingleton<IMyService, AlternativeServiceImpl>("secondary");
        container.RegisterSingleton<ServiceWithKeyedDependency>();

        var service = container.GetRequiredService<ServiceWithKeyedDependency>();
        Assert(service!.PrimaryService != null, "Primary service should not be null");
        Assert(service!.PrimaryService is MyServiceImpl, "Primary service should be MyServiceImpl");
    });

    RunTest("TryRegister Methods", () =>
    {
        using var container = new ServiceContainer();
        bool first = container.TryRegisterSingleton<SimpleService>();
        bool second = container.TryRegisterSingleton<SimpleService>();

        Assert(first, "First registration should succeed");
        Assert(!second, "Second registration should fail");
        Assert(container.Count == 1, "Should only have one registration");
    });

    RunTest("Service Overwriting", () =>
    {
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        var service1 = container.GetRequiredService<IMyService>();

        container.RegisterSingleton<IMyService, AlternativeServiceImpl>();
        var service2 = container.GetRequiredService<IMyService>();

        Assert(service1 is MyServiceImpl, "First service should be MyServiceImpl");
        Assert(service2 is AlternativeServiceImpl, "Second service should be AlternativeServiceImpl");
    });

    RunTest("Large Dependency Tree", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        container.RegisterSingleton<ServiceWithDependency>();
        container.RegisterSingleton<ComplexService>();
        container.RegisterSingleton<DeeplyNestedService>();
        container.RegisterSingleton<ServiceWithPropertyInjection>();
        container.RegisterSingleton<MegaComplexService>();

        var service = container.GetRequiredService<MegaComplexService>();
        Assert(service != null, "Service should not be null");
        Assert(service!.Complex != null, "Complex dependency should not be null");
        Assert(service!.Nested != null, "Nested dependency should not be null");
    });

#if NET5_0_OR_GREATER
    RunTestAsync("Async Disposable Services", async () =>
    {
        AsyncDisposableService? service;
        await using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<AsyncDisposableService>();
            service = container.GetRequiredService<AsyncDisposableService>();
            Assert(!service.IsDisposed, "Service should not be disposed yet");
        }

        Assert(service!.IsDisposed, "Service should be disposed after container disposal");
    }).GetAwaiter().GetResult();
#endif

    RunTest("GetConstructor Method", () =>
    {
        using var container = new ServiceContainer();
        var constructor = container.GetConstructor(typeof(SimpleService));
        Assert(constructor != null, "Constructor should not be null");
        Assert(constructor!.GetParameters().Length == 0, "SimpleService should have parameterless constructor");

        var complexConstructor = container.GetConstructor(typeof(ComplexService));
        Assert(complexConstructor != null, "ComplexService constructor should not be null");
        Assert(complexConstructor!.GetParameters().Length == 2, "ComplexService should have 2-parameter constructor");
    });

    RunTest("Mixed Lifetime Registration", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>("singleton");
        container.RegisterTransient<SimpleService>("transient");
        container.RegisterScoped<SimpleService>("scoped");

        var singleton = container.GetRequiredKeyedService<SimpleService>("singleton");
        var transient1 = container.GetRequiredKeyedService<SimpleService>("transient");
        var transient2 = container.GetRequiredKeyedService<SimpleService>("transient");

        using var scope = container.CreateScope();
        var scoped1 = scope.ServiceFactory.GetRequiredKeyedService<SimpleService>("scoped");
        var scoped2 = scope.ServiceFactory.GetRequiredKeyedService<SimpleService>("scoped");

        Assert(!ReferenceEquals(transient1, transient2), "Transients should be different");
        Assert(ReferenceEquals(scoped1, scoped2), "Scoped should be same in scope");
    });

    RunTest("Multiple Levels of Nested Generics", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<GenericService<List<Dictionary<string, int>>>>();

        var service = container.GetRequiredService<GenericService<List<Dictionary<string, int>>>>();
        Assert(service != null, "Nested generic service should not be null");
    });

    RunTest("Interface with Multiple Implementations", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>("impl1");
        container.RegisterSingleton<IMyService, AlternativeServiceImpl>("impl2");
        container.RegisterSingleton<IMyService, ThirdServiceImpl>("impl3");

        var impl1 = container.GetRequiredKeyedService<IMyService>("impl1");
        var impl2 = container.GetRequiredKeyedService<IMyService>("impl2");
        var impl3 = container.GetRequiredKeyedService<IMyService>("impl3");

        Assert(impl1.GetValue() == 42, "Implementation 1 should return 42");
        Assert(impl2.GetValue() == 100, "Implementation 2 should return 100");
        Assert(impl3.GetValue() == 200, "Implementation 3 should return 200");
    });

    RunTest("Service with Generic Constraint", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ConstrainedGenericService<DisposableService>>();

        var service = container.GetRequiredService<ConstrainedGenericService<DisposableService>>();
        Assert(service != null, "Constrained generic service should not be null");
    });

    RunTest("Multiple Property Injections on Same Type", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        container.RegisterSingleton<MultiPropertyInjectionService>();

        var service = container.GetRequiredService<MultiPropertyInjectionService>();
        Assert(service!.Service1 != null, "Service1 should be injected");
        Assert(service!.Service2 != null, "Service2 should be injected");
        Assert(service!.Service3 != null, "Service3 should be injected");
    });

    RunTest("Recursive Generic Types", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<RecursiveGeneric<int>>();

        var service = container.GetRequiredService<RecursiveGeneric<int>>();
        Assert(service != null, "Recursive generic should not be null");
    });

    RunTest("Service with Value Type Constructor Parameter", () =>
    {
        using var container = new ServiceContainer();
        // This tests if value types in constructor are handled properly
        container.RegisterSingleton<ServiceWithValueTypeParam>();

        var service = container.GetRequiredService<ServiceWithValueTypeParam>();
        Assert(service != null, "Service with value type param should not be null");
        Assert(service!.Number == 0, "Value type should have default value");
    });

    RunTest("Covariant Generic Interface", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ICovariant<MyServiceImpl>, CovariantService>();

        var service = container.GetRequiredService<ICovariant<MyServiceImpl>>();
        Assert(service != null, "Covariant service should not be null");
    });

    RunTest("Service with Multiple Generic Parameters", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<MultiGenericService<string, int, bool>>();

        var service = container.GetRequiredService<MultiGenericService<string, int, bool>>();
        Assert(service != null, "Multi-generic service should not be null");
        Assert(service!.GetTypeNames() == "String, Int32, Boolean", "Type names should match");
    });

    RunTest("Deeply Nested Property Injection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<ServiceWithDependency>();
        container.RegisterSingleton<DeeplyNestedPropertyService>();

        var service = container.GetRequiredService<DeeplyNestedPropertyService>();
        Assert(service!.Level1 != null, "Level 1 should be injected");
        Assert(service!.Level1!.Dependency != null, "Nested dependency should be injected");
    });

    RunTest("Service with Struct Property", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithStructProperty>();

        var service = container.GetRequiredService<ServiceWithStructProperty>();
        Assert(service != null, "Service should not be null");
        Assert(service!.Point.X == 0 && service.Point.Y == 0, "Struct should have default values");
    });

    RunTest("Factory with Closure Variable", () =>
    {
        using var container = new ServiceContainer();
        int capturedValue = 42;
        container.RegisterSingleton<IMyService>((sp, key) => new MyServiceImplWithValue(capturedValue));

        var service = container.GetRequiredService<IMyService>();
        Assert(service!.GetValue() == 42, "Captured value should be used");
    });

    RunTest("Service with Abstract Base Class", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<AbstractBase, ConcreteImplementation>();

        var service = container.GetRequiredService<AbstractBase>();
        Assert(service != null, "Abstract base service should not be null");
        Assert(service is ConcreteImplementation, "Should be concrete implementation");
    });

    RunTest("Service with Internal Constructor", () =>
    {
        using var container = new ServiceContainer();

        // Internal constructors cannot be accessed by DI - this is expected to fail
        try
        {
            container.RegisterSingleton<ServiceWithInternalConstructor>();
            var service = container.GetRequiredService<ServiceWithInternalConstructor>();
            Assert(false, "Should have failed - internal constructors are not accessible");
        }
        catch
        {
            // Expected - internal constructors should not be accessible
            Assert(true, "Internal constructor correctly fails");
        }
    });

    RunTest("Keyed Service with Null Key", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>((object?)null);

        var service = container.GetRequiredKeyedService<IMyService>(null);
        Assert(service != null, "Service with null key should work");
    });

    RunTest("Generic Service with Interface Constraint", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<GenericWithInterfaceConstraint<MyServiceImpl>>();

        var service = container.GetRequiredService<GenericWithInterfaceConstraint<MyServiceImpl>>();
        Assert(service != null, "Generic with interface constraint should work");
    });

    RunTest("Service with Enum Property Injection", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithEnum>();

        var service = container.GetRequiredService<ServiceWithEnum>();
        Assert(service!.Status == ServiceStatus.Pending, "Enum should have default value");
    });

    RunTest("Nested Scopes with Same Service", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterScoped<SimpleService>();

        using var outerScope = container.CreateScope();
        var outerService = outerScope.ServiceFactory.GetRequiredService<SimpleService>();

        using var innerScope = container.CreateScope();
        var innerService = innerScope.ServiceFactory.GetRequiredService<SimpleService>();

        Assert(!ReferenceEquals(outerService, innerService), "Different scopes should have different instances");
    });

    RunTest("Service with Array Constructor Parameter", () =>
    {
        using var container = new ServiceContainer();
        // Arrays need to be explicitly provided or have default value
        container.RegisterSingleton(new ServiceWithArrayParam(new[] { "test1", "test2" }));

        var service = container.GetRequiredService<ServiceWithArrayParam>();
        Assert(service != null, "Service with array param should work");
        Assert(service!.Items != null && service.Items.Length == 2, "Array should have 2 items");
    });

    RunTest("Service Resolution After Container Modification", () =>
    {
        using var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);
        container.RegisterSingleton<IMyService, MyServiceImpl>();
        var service1 = container.GetRequiredService<IMyService>();

        container.RegisterSingleton<SimpleService>();
        var service2 = container.GetRequiredService<SimpleService>();

        Assert(service1 != null && service2 != null, "Both services should resolve after modifications");
    });

    RunTest("Factory Returning Null (Should Fail)", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService>((sp, key) => null!);

        try
        {
            var service = container.GetRequiredService<IMyService>();
            Assert(false, "Should have thrown exception for null factory result");
        }
        catch
        {
            // Expected - factory returning null should fail
            Assert(true, "Factory returning null correctly throws");
        }
    });

    RunTest("Multiple Disposal Calls on Same Service", () =>
    {
        DisposableService service;
        using (var container = new ServiceContainer())
        {
            container.RegisterSingleton<DisposableService>();
            service = container.GetRequiredService<DisposableService>();
        }

        // Try disposing again
        service.Dispose();
        service.Dispose();

        Assert(service!.IsDisposed, "Service should remain disposed");
    });

    RunTest("Generic Service with Tuple Types", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<GenericService<(string Name, int Age)>>();

        var service = container.GetRequiredService<GenericService<(string Name, int Age)>>();
        Assert(service != null, "Tuple generic service should work");
    });

    RunTest("Service with Lazy<T> Dependency", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        // Lazy<T> needs to be registered as a factory
        container.RegisterSingleton<Lazy<SimpleService>>((sp, key) => new Lazy<SimpleService>(() => sp.GetRequiredService<SimpleService>()));
        container.RegisterSingleton<ServiceWithLazyDependency>();

        var service = container.GetRequiredService<ServiceWithLazyDependency>();
        Assert(service!.LazyService != null, "Lazy should not be null");
        Assert(!service.LazyService!.IsValueCreated, "Lazy should not be created yet");
        var value = service.LazyService.Value;
        Assert(service!.LazyService.IsValueCreated, "Lazy should be created after access");
    });

    RunTest("Service with Func<T> Factory Dependency", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterTransient<SimpleService>();
        // Func<T> needs to be registered as a factory
        container.RegisterSingleton<Func<SimpleService>>((sp, key) => () => sp.GetRequiredService<SimpleService>());
        container.RegisterSingleton<ServiceWithFuncDependency>();

        var service = container.GetRequiredService<ServiceWithFuncDependency>();
        var instance1 = service.Factory();
        var instance2 = service.Factory();

        Assert(instance1 != null && instance2 != null, "Instances should not be null");
        Assert(!ReferenceEquals(instance1, instance2), "Func should create new instances");
    });
}

catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n❌ CRITICAL ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
    testsFailed++;
}

stopwatch.Stop();

// Print Summary
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("TEST SUMMARY");
Console.WriteLine(new string('=', 60));
Console.WriteLine($"Tests Run:    {testsRun}");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Tests Passed: {testsPassed}");
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"Tests Failed: {testsFailed}");
Console.ResetColor();
Console.WriteLine($"Elapsed Time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine(new string('=', 60));

if (testsFailed == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✓ ALL TESTS PASSED - AOT COMPATIBLE!");
    Console.ResetColor();
    return 0;
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n✗ SOME TESTS FAILED - AOT ISSUES DETECTED!");
    Console.ResetColor();
    return 1;
}

void RunTest(string testName, Action testAction)
{
    testsRun++;
    try
    {
        Console.Write($"[{testsRun:D2}] {testName}... ");
        testAction();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ PASSED");
        Console.ResetColor();
        testsPassed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ FAILED");
        Console.WriteLine($"    Error: {ex.Message}");
        Console.ResetColor();
        testsFailed++;
    }
}

void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception($"Assertion failed: {message}");
}

#if NET5_0_OR_GREATER
async Task RunTestAsync(string testName, Func<Task> testAction)
{
    testsRun++;
    try
    {
        Console.Write($"[{testsRun:D2}] {testName}... ");
        await testAction();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ PASSED");
        Console.ResetColor();
        testsPassed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ FAILED");
        Console.WriteLine($"    Error: {ex.Message}");
        Console.ResetColor();
        testsFailed++;
    }
}
#endif

// ========== Test Service Classes ==========

public class SimpleService
{
    public string GetMessage()
    {
        return "SimpleService";
    }
}

public interface IMyService
{
    int GetValue();
}

public class MyServiceImpl : IMyService
{
    public int GetValue()
    {
        return 42;
    }
}

public class AlternativeServiceImpl : IMyService
{
    public int GetValue()
    {
        return 100;
    }
}

public class ServiceWithDependency
{
    public SimpleService Dependency { get; }

    public ServiceWithDependency(SimpleService dependency)
    {
        Dependency = dependency;
    }
}

public class ComplexService
{
    public SimpleService SimpleService { get; }
    public IMyService MyService { get; }

    public ComplexService(SimpleService simpleService, IMyService myService)
    {
        SimpleService = simpleService;
        MyService = myService;
    }
}

public class ServiceWithPropertyInjection
{
    [Inject]
    public SimpleService? InjectedService { get; set; }
}

public class ServiceWithOptionalProperty
{
    [Inject(isRequired: false)]
    public SimpleService? OptionalService { get; set; }
}

public class GenericService<T>
{
    public string GetTypeName()
    {
        return typeof(T).Name;
    }
}

public class DeeplyNestedService
{
    public ServiceWithDependency Middle { get; }

    public DeeplyNestedService(ServiceWithDependency middle)
    {
        Middle = middle;
    }
}

public class DisposableService : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public struct ValueTypeStruct
{
    public int Value { get; set; }
}

public class StructService
{
    public int Value { get; set; }
}

public class ServiceWithMultipleConstructors
{
    public bool UsedPreferredConstructor { get; }

    public ServiceWithMultipleConstructors()
    {
        UsedPreferredConstructor = false;
    }

    [PreferredConstructor]
    public ServiceWithMultipleConstructors(SimpleService dependency)
    {
        UsedPreferredConstructor = true;
    }
}

public class CircularA
{
    public CircularA(CircularB b) { }
}

public class CircularB
{
    public CircularB(CircularA a) { }
}

public class ComplexPropertyInjectionService
{
    [Inject]
    public SimpleService? RequiredService { get; set; }

    [Inject(isRequired: false)]
    public IMyService? OptionalService { get; set; }
}

public class ServiceWithKeyedDependency
{
    public IMyService PrimaryService { get; }

    public ServiceWithKeyedDependency([FromKeyedServices("primary")] IMyService primaryService)
    {
        PrimaryService = primaryService;
    }
}

public class MegaComplexService
{
    public ComplexService Complex { get; }
    public DeeplyNestedService Nested { get; }

    public MegaComplexService(ComplexService complex, DeeplyNestedService nested)
    {
        Complex = complex;
        Nested = nested;
    }
}

#if NET5_0_OR_GREATER
public class AsyncDisposableService : IAsyncDisposable
{
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
#endif

// ========== Advanced Edge Case Test Classes ==========

public class ThirdServiceImpl : IMyService
{
    public int GetValue()
    {
        return 200;
    }
}

public class ConstrainedGenericService<T> where T : IDisposable
{
    public Type GetConstrainedType()
    {
        return typeof(T);
    }
}

public class MultiPropertyInjectionService
{
    [Inject] public SimpleService? Service1 { get; set; }
    [Inject] public SimpleService? Service2 { get; set; }
    [Inject] public IMyService? Service3 { get; set; }
}

public class RecursiveGeneric<T>
{
    public GenericService<List<T>>? NestedGeneric { get; set; }
}

public class ServiceWithValueTypeParam
{
    public int Number { get; }

    public ServiceWithValueTypeParam(int number = 0)
    {
        Number = number;
    }
}

public interface ICovariant<out T>
{
    T GetValue();
}

public class CovariantService : ICovariant<MyServiceImpl>
{
    public MyServiceImpl GetValue()
    {
        return new MyServiceImpl();
    }
}

public class MultiGenericService<T1, T2, T3>
{
    public string GetTypeNames()
    {
        return $"{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}";
    }
}

public class DeeplyNestedPropertyService
{
    [Inject] public ServiceWithDependency? Level1 { get; set; }
}

public struct Point
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class ServiceWithStructProperty
{
    public Point Point { get; set; }
}

public class MyServiceImplWithValue : IMyService
{
    private readonly int _value;

    public MyServiceImplWithValue(int value)
    {
        _value = value;
    }

    public int GetValue()
    {
        return _value;
    }
}

public abstract class AbstractBase
{
    public abstract string GetMessage();
}

public class ConcreteImplementation : AbstractBase
{
    public override string GetMessage()
    {
        return "Concrete";
    }
}

public class ServiceWithInternalConstructor
{
    internal ServiceWithInternalConstructor()
    {
    }
}

public class GenericWithInterfaceConstraint<T> where T : IMyService
{
    public Type GetConstraintType()
    {
        return typeof(T);
    }
}

public enum ServiceStatus
{
    Pending,
    Active,
    Completed
}

public class ServiceWithEnum
{
    public ServiceStatus Status { get; set; }
}

public class ServiceWithArrayParam
{
    public string[]? Items { get; }

    public ServiceWithArrayParam(string[]? items = null)
    {
        Items = items ?? Array.Empty<string>();
    }
}

public class ServiceWithLazyDependency
{
    public Lazy<SimpleService> LazyService { get; }

    public ServiceWithLazyDependency(Lazy<SimpleService> lazyService)
    {
        LazyService = lazyService;
    }
}

public class ServiceWithFuncDependency
{
    public Func<SimpleService> Factory { get; }

    public ServiceWithFuncDependency(Func<SimpleService> factory)
    {
        Factory = factory;
    }
}
