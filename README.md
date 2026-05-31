[![License](https://img.shields.io/github/license/snowberry-software/DependencyInjection)](https://github.com/snowberry-software/DependencyInjection/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Snowberry.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/Snowberry.DependencyInjection/)

# Snowberry.DependencyInjection

A lightweight, easy-to-use IoC container for .NET. Warm resolution runs through a compiled per-service resolver graph (allocation parity with `Microsoft.Extensions.DependencyInjection`), and unlike build-once providers the container stays **mutable**, so you can add, remove, and overwrite registrations at any time. When you are done configuring, an opt-in `Freeze()` locks it in for maximum speed.

## Install

```
dotnet add package Snowberry.DependencyInjection
```

## Quick start

```cs
using Snowberry.DependencyInjection;
using Snowberry.DependencyInjection.Abstractions.Extensions;

using var container = new ServiceContainer();

container.RegisterSingleton<IFoo, Foo>();
container.RegisterTransient<IBar, Bar>();

var foo = container.GetRequiredService<IFoo>();
```

Dispose the container (`Dispose()` or `await container.DisposeAsync()`) when you are done. It disposes the instances it created.

## Service lifetimes

| Lifetime  | Description                                        |
| --------- | -------------------------------------------------- |
| Singleton | One instance for the container's lifetime.         |
| Transient | A new instance on every resolve.                   |
| Scoped    | One instance per scope.                            |

```cs
container.RegisterSingleton<IFoo, Foo>();
container.RegisterTransient<IBar, Bar>();
container.RegisterScoped<IBaz, Baz>();
```

Register a pre-built instance. Instances **you** supply are not disposed by the container:

```cs
container.RegisterSingleton<IFoo>(instance: new Foo());
```

## Scopes

```cs
container.RegisterScoped<IScopedType, ScopedType>();

using (var scope = container.CreateScope())
{
    // Created for this scope and disposed when the scope is disposed.
    var svc = scope.ServiceProvider.GetRequiredService<IScopedType>();
}
```

Resolving a scoped service directly from the container uses the container's own root scope (disposed with the container).

## Keyed services

```cs
container.RegisterTransient<ITestService, TestServiceA>("_KEY0_");

var svc = container.GetRequiredKeyedService<ITestService>("_KEY0_");
```

## Open generic types

```cs
container.Register(typeof(IRepository<>), typeof(Repository<>), serviceKey: null, lifetime: ServiceLifetime.Transient, singletonInstance: null);

var repo = container.GetRequiredService<IRepository<User>>();
```

## Overwriting registrations

By default a registration can be replaced. Pass `ServiceContainerOptions.ReadOnly` to forbid overwrites.

```cs
var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);

container.RegisterTransient<IService, Impl>();
// Replaces the registration. Previously-created instances are still disposed as usual.
container.RegisterSingleton<IService, OtherImpl>();
```

## Injection attributes

### `InjectAttribute` (`[Inject]`)

Injects a service into a **property** during construction.

- Valid on properties only. Cannot be applied more than once. Inherited.
- Required by default. If the service is not registered, resolution throws. Pass `isRequired: false` to make it optional, in which case an unregistered service leaves the property as its default (`null`) instead of throwing.
- Can be combined with `[FromKeyedServices]`.

```cs
[Inject]
public ITestService Service { get; set; }

[Inject(isRequired: false)]
public ILogger? OptionalLogger { get; set; }
```

### `FromKeyedServicesAttribute` (`[FromKeyedServices]`)

Selects which keyed service to use for a **property or constructor parameter**.

- Valid on properties and parameters. Cannot be applied more than once. Inherited.
- On a property, combine with `[Inject]`; on a constructor parameter it works on its own.

```cs
// Property:
[Inject]
[FromKeyedServices("_KEY1_")]
public ITestService? KeyedService { get; set; }

// Constructor parameter:
public MyService([FromKeyedServices("_KEY1_")] ITestService service) { ... }
```

### `PreferredConstructorAttribute` (`[PreferredConstructor]`)

Specifies which **constructor** the container should use to instantiate a type.

- Valid on constructors only. Cannot be applied more than once. Not inherited.
- Not needed when the type has a single constructor.

```cs
public class MyService
{
    public MyService() { }

    [PreferredConstructor]
    public MyService(IDependency dependency) { ... }
}
```

## Validation

Check the whole registered graph up front ("fail fast at startup") without constructing any instances. The container stays mutable; re-run after further changes.

```cs
// Throws ServiceValidationException listing every problem.
container.Validate();

// Or collect problems without throwing.
if (!container.TryValidate(out var errors))
{
    foreach (var error in errors)
        Console.WriteLine(error); // missing dependency / circular dependency / no usable constructor
}
```

Missing required dependencies and dependency cycles surface as exceptions at resolve time too (`ServiceTypeNotRegistered`, `CircularDependencyException`).

## Freezing (opt-in lock-in for maximum speed)

When configuration is complete, `Freeze()` locks the container for the fastest resolves:

```cs
container.Freeze();                 // validates first; pass Freeze(validate: false) to skip
bool locked = container.IsFrozen;

container.RegisterTransient<IBaz, Baz>(); // throws ServiceRegistryReadOnlyException
```

- **`Freeze()` validates by default.** It runs `Validate()` first; on a problem it throws `ServiceValidationException` and the container stays **unfrozen** and mutable. Use `Freeze(validate: false)` to skip.
- One-way and idempotent (re-freezing is a no-op and does not re-validate).
- Stronger than `ServiceContainerOptions.ReadOnly`, which blocks overwrite only. Freezing blocks **all** registration changes.
- Freeze before creating long-lived scopes (scopes created after freezing use the optimized scope cache).

| Mode                | Add / remove / overwrite | Warm-resolve speed                                                  |
| ------------------- | ------------------------ | ------------------------------------------------------------------- |
| Mutable (default)   | Yes                      | Fast: beats MS.DI on singleton/scoped, parity on simple transients  |
| Frozen (`Freeze()`) | No (locked)              | Fastest: full-graph inlining, baked singletons, optimized scopes    |

## How it works

Resolution is backed by a compiled per-service resolver graph: the first resolve of a type compiles and caches a delegate, and every warm resolve afterward is a dictionary lookup plus a delegate call (0 allocations for warm singleton/scoped). See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for diagrams of the resolver graph and the mutable → frozen lifecycle.
