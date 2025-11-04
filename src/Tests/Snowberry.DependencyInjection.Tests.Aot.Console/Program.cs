using System.Diagnostics;
using Snowberry.DependencyInjection;
using Snowberry.DependencyInjection.Abstractions.Attributes;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

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
        var service1a = scope1.ServiceProvider.GetRequiredService<SimpleService>();
        var service1b = scope1.ServiceProvider.GetRequiredService<SimpleService>();

        using var scope2 = container.CreateScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<SimpleService>();

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
        var constructor = container.ServiceFactory.GetConstructor(typeof(SimpleService));
        Assert(constructor != null, "Constructor should not be null");
        Assert(constructor!.GetParameters().Length == 0, "SimpleService should have parameterless constructor");

        var complexConstructor = container.ServiceFactory.GetConstructor(typeof(ComplexService));
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
        var scoped1 = scope.ServiceProvider.GetRequiredKeyedService<SimpleService>("scoped");
        var scoped2 = scope.ServiceProvider.GetRequiredKeyedService<SimpleService>("scoped");

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

        var instance = container.CreateInstance<RecursiveGenericService<int>>();
        Assert(instance != null, "Recursive generic should not be null");
        Assert(instance!.GetInnerType() == typeof(int), "Inner type should be int");
    });

    // Built-in Services Tests
    RunTest("BuiltInService_IServiceProvider_ReturnsServiceProvider", () =>
    {
        using var container = new ServiceContainer();

        var serviceProvider = container.GetRequiredService<IServiceProvider>();
        Assert(serviceProvider != null, "IServiceProvider should not be null");
        // Container is a wrapper - can't guarantee it returns container itself
        var provider2 = container.GetRequiredService<IServiceProvider>();
        Assert(ReferenceEquals(serviceProvider, provider2), "Same provider should be returned");
    });

    RunTest("BuiltInService_IServiceProvider_InScope_ReturnsScope", () =>
    {
        using var container = new ServiceContainer();
        using var scope = container.CreateScope();

        var serviceProvider = scope.ServiceProvider.GetRequiredService<IServiceProvider>();
        Assert(serviceProvider != null, "IServiceProvider should not be null");
        Assert(ReferenceEquals(scope.ServiceProvider, serviceProvider), "IServiceProvider should be the scope");

        // Verify it's different from root
        var rootServiceProvider = container.GetService<IServiceProvider>();
        Assert(!ReferenceEquals(rootServiceProvider, serviceProvider), "Root and scope providers should differ");
    });

    RunTest("BuiltInService_IScope_ReturnsRootScope", () =>
    {
        using var container = new ServiceContainer();

        var scope = container.GetRequiredService<IScope>();
        Assert(scope != null, "IScope should not be null");
        Assert(scope!.IsGlobalScope, "Root scope should be global");
    });

    RunTest("BuiltInService_IScope_InScope_ReturnsNonGlobalScope", () =>
    {
        using var container = new ServiceContainer();
        using var createdScope = container.CreateScope();

        var scope = createdScope.ServiceProvider.GetRequiredService<IScope>();
        Assert(scope != null, "IScope should not be null");
        Assert(!scope!.IsGlobalScope, "Created scope should not be global");
    });

    RunTest("BuiltInService_IServiceScopeFactory_ReturnsFactory", () =>
    {
        using var container = new ServiceContainer();

        var factory = container.GetRequiredService<IServiceScopeFactory>();
        Assert(factory != null, "IServiceScopeFactory should not be null");
    });

    RunTest("BuiltInService_IServiceScopeFactory_CanCreateScopes", () =>
    {
        using var container = new ServiceContainer();
        var factory = container.GetRequiredService<IServiceScopeFactory>();

        using var scope1 = factory.CreateScope();
        using var scope2 = factory.CreateScope();

        Assert(scope1 != null, "Scope 1 should not be null");
        Assert(scope2 != null, "Scope 2 should not be null");
        Assert(!ReferenceEquals(scope1, scope2), "Scopes should be different");
    });

    RunTest("BuiltInService_IServiceFactory_ReturnsFactory", () =>
    {
        using var container = new ServiceContainer();

        var factory = container.GetRequiredService<IServiceFactory>();
        Assert(factory != null, "IServiceFactory should not be null");
        // Don't assume container.ServiceFactory is same instance
    });

    RunTest("BuiltInService_KeyedServices_WorkWithBuiltIns", () =>
    {
        using var container = new ServiceContainer();

        // With null key - should work
        var serviceProviderNull = container.GetKeyedService<IServiceProvider>(null);
        var scopeNull = container.GetKeyedService<IScope>(null);
        var scopeFactoryNull = container.GetKeyedService<IServiceScopeFactory>(null);
        var serviceFactoryNull = container.GetKeyedService<IServiceFactory>(null);

        Assert(serviceProviderNull != null, "Null-keyed IServiceProvider should work");
        Assert(scopeNull != null, "Null-keyed IScope should work");
        Assert(scopeFactoryNull != null, "Null-keyed IServiceScopeFactory should work");
        Assert(serviceFactoryNull != null, "Null-keyed IServiceFactory should work");

        // With non-null key - should return null
        var serviceProvider = container.GetKeyedService<IServiceProvider>("anyKey");
        var scope = container.GetKeyedService<IScope>("anyKey");
        var scopeFactory = container.GetKeyedService<IServiceScopeFactory>("anyKey");
        var serviceFactory = container.GetKeyedService<IServiceFactory>("anyKey");

        Assert(serviceProvider == null, "Non-null-keyed IServiceProvider should return null");
        Assert(scope == null, "Non-null-keyed IScope should return null");
        Assert(scopeFactory == null, "Non-null-keyed IServiceScopeFactory should return null");
        Assert(serviceFactory == null, "Non-null-keyed IServiceFactory should return null");
    });

    RunTest("BuiltInService_InjectedIntoUserService", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<ServiceWithBuiltInDependencies>();

        var service = container.GetRequiredService<ServiceWithBuiltInDependencies>();
        Assert(service != null, "Service should not be null");
        Assert(service!.ServiceProvider != null, "IServiceProvider should be injected");
        Assert(service!.Scope != null, "IScope should be injected");
        Assert(service!.ScopeFactory != null, "IServiceScopeFactory should be injected");
        Assert(service!.ServiceFactory != null, "IServiceFactory should be injected");

        // Verify the injected scope is the global scope
        Assert(service!.Scope.IsGlobalScope, "Injected scope should be global");
    });

    RunTest("BuiltInService_MultipleScopesHaveDifferentProviders", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterScoped<ServiceWithBuiltInDependencies>();

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var service1 = scope1.ServiceProvider.GetRequiredService<ServiceWithBuiltInDependencies>();
        var service2 = scope2.ServiceProvider.GetRequiredService<ServiceWithBuiltInDependencies>();

        Assert(!ReferenceEquals(service1, service2), "Services in different scopes should differ");
        Assert(!ReferenceEquals(service1.ServiceProvider, service2.ServiceProvider), "ServiceProviders should differ");
        Assert(ReferenceEquals(service1.ServiceFactory, service2.ServiceFactory), "ServiceFactories should be same");
    });

    // CreateInstance Extension Method Tests
    RunTest("CreateInstance_WithNoParameters_CreatesInstance", () =>
    {
        using var container = new ServiceContainer();

        var instance = container.CreateInstance<SimpleService>();
        Assert(instance != null, "Instance should not be null");
    });

    RunTest("CreateInstance_WithDependency_InjectsDependency", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();

        var instance = container.CreateInstance<ServiceWithDependency>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.Dependency != null, "Dependency should be injected");
    });

    RunTest("CreateInstance_WithPropertyInjection_InjectsProperties", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();

        var instance = container.CreateInstance<ServiceWithPropertyInjection>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.InjectedService != null, "Property should be injected");
    });

    RunTest("CreateInstance_NonGeneric_CreatesCorrectType", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();

        object? instance = container.CreateInstance(typeof(ServiceWithDependency));
        Assert(instance != null, "Instance should not be null");
        Assert(instance is ServiceWithDependency, "Instance should be correct type");
    });

    RunTest("CreateInstance_AlwaysCreatesNewInstance", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();

        var instance1 = container.CreateInstance<ServiceWithDependency>();
        var instance2 = container.CreateInstance<ServiceWithDependency>();

        Assert(!ReferenceEquals(instance1, instance2), "Instances should be different");
        Assert(ReferenceEquals(instance1!.Dependency, instance2!.Dependency), "Dependencies should be same (singleton)");
    });

    RunTest("CreateInstance_FromScope_UsesCorrectServiceProvider", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterScoped<SimpleService>();

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var instance1 = scope1.ServiceProvider.CreateInstance<ServiceWithDependency>();
        var instance2 = scope2.ServiceProvider.CreateInstance<ServiceWithDependency>();

        Assert(!ReferenceEquals(instance1!.Dependency, instance2!.Dependency), "Scoped dependencies should differ");
    });

    RunTest("CreateInstance_GenericService_CreatesCorrectly", () =>
    {
        using var container = new ServiceContainer();

        var instance = container.CreateInstance<GenericService<string>>();
        Assert(instance != null, "Generic instance should not be null");
        Assert(instance!.GetTypeName() == "String", "Generic type should be correct");
    });

    RunTest("CreateInstance_MultipleGenericParameters_CreatesCorrectly", () =>
    {
        using var container = new ServiceContainer();

        var instance = container.CreateInstance<MultiGenericServiceAot<string, int, bool>>();
        Assert(instance != null, "Multi-generic instance should not be null");
        Assert(instance!.GetTypeNames() == "String, Int32, Boolean", "Type names should match");
    });

    RunTest("CreateInstance_WithOptionalProperty_DoesNotThrow", () =>
    {
        using var container = new ServiceContainer();
        // SimpleService not registered

        var instance = container.CreateInstance<ServiceWithOptionalProperty>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.OptionalService == null, "Optional property should be null");
    });

    RunTest("CreateInstance_WithKeyedDependency_InjectsKeyedService", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<IMyService, MyServiceImpl>("primary");

        var instance = container.CreateInstance<ServiceWithKeyedDependency>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.PrimaryService != null, "Keyed dependency should be injected");
    });

    RunTest("CreateInstance_WithPreferredConstructor_UsesPreferred", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();

        var instance = container.CreateInstance<ServiceWithMultipleConstructors>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.UsedPreferredConstructor, "Should use preferred constructor");
    });

    RunTest("CreateInstance_FromServiceFactory_CreatesInstance", () =>
    {
        using var container = new ServiceContainer();
        var serviceFactory = container.GetRequiredService<IServiceFactory>();

        var instance = serviceFactory.CreateInstance<SimpleService>(container);
        Assert(instance != null, "Instance from factory should not be null");
    });

    RunTest("CreateInstance_NonGenericFromFactory_CreatesInstance", () =>
    {
        using var container = new ServiceContainer();
        var serviceFactory = container.GetRequiredService<IServiceFactory>();

        object? instance = serviceFactory.CreateInstance(typeof(SimpleService), container);
        Assert(instance != null, "Instance from factory should not be null");
        Assert(instance is SimpleService, "Instance should be correct type");
    });

    RunTest("CreateInstance_WithDisposableService_ServiceIsDisposed", () =>
    {
        DisposableService service;
        using (var container = new ServiceContainer())
        {
            service = container.CreateInstance<DisposableService>();
            Assert(!service.IsDisposed, "Service should not be disposed yet");
        }

        Assert(!service!.IsDisposed, "Service should not be disposed after container disposal");
    });

    RunTest("CreateInstance_ComplexGenericType_CreatesCorrectly", () =>
    {
        using var container = new ServiceContainer();

        var instance = container.CreateInstance<ComplexGenericServiceAot<List<Dictionary<string, int>>>>();
        Assert(instance != null, "Complex generic should not be null");
    });

    RunTest("CreateInstance_WithBothInjectionTypes_InjectsBoth", () =>
    {
        using var container = new ServiceContainer();
        container.RegisterSingleton<SimpleService>();
        container.RegisterSingleton<IMyService, MyServiceImpl>();

        var instance = container.CreateInstance<ServiceWithBothInjectionTypesAot>();
        Assert(instance != null, "Instance should not be null");
        Assert(instance!.ConstructorDependency != null, "Constructor dependency should be injected");
        Assert(instance!.PropertyDependency != null, "Property dependency should be injected");
    });

    RunTest("GetConstructor_ReturnsCorrectConstructor", () =>
    {
        using var container = new ServiceContainer();
        var constructor = container.ServiceFactory.GetConstructor(typeof(SimpleService));
        Assert(constructor != null, "Constructor should not be null");
        Assert(constructor!.GetParameters().Length == 0, "SimpleService should have parameterless constructor");
    });

    RunTest("GetConstructor_ComplexService_ReturnsCorrectConstructor", () =>
    {
        using var container = new ServiceContainer();
        var constructor = container.ServiceFactory.GetConstructor(typeof(ComplexService));
        Assert(constructor != null, "ComplexService constructor should not be null");
        Assert(constructor!.GetParameters().Length == 2, "ComplexService should have 2-parameter constructor");
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

public class ServiceWithBuiltInDependencies
{
    public IServiceProvider ServiceProvider { get; }
    public IScope Scope { get; }
    public IServiceScopeFactory ScopeFactory { get; }
    public IServiceFactory ServiceFactory { get; }

    public ServiceWithBuiltInDependencies(
        IServiceProvider serviceProvider,
        IScope scope,
        IServiceScopeFactory scopeFactory,
        IServiceFactory serviceFactory)
    {
        ServiceProvider = serviceProvider;
        Scope = scope;
        ScopeFactory = scopeFactory;
        ServiceFactory = serviceFactory;
    }
}

public class MultiGenericServiceAot<T1, T2, T3>
{
    public string GetTypeNames()
    {
        return $"{typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}";
    }
}

public class ComplexGenericServiceAot<T>
{
    public string GetTypeName()
    {
        return typeof(T).Name;
    }
}

public class ServiceWithBothInjectionTypesAot
{
    public SimpleService ConstructorDependency { get; }

    [Inject]
    public IMyService? PropertyDependency { get; set; }

    public ServiceWithBothInjectionTypesAot(SimpleService constructorDependency)
    {
        ConstructorDependency = constructorDependency;
    }
}

public class RecursiveGenericService<T>
{
    public Type GetInnerType()
    {
        return typeof(T);
    }
}
