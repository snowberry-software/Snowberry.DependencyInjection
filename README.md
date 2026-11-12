# Snowberry.DependencyInjection

[![License](https://img.shields.io/github/license/snowberry-software/DependencyInjection)](https://github.com/snowberry-software/DependencyInjection/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Snowberry.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/Snowberry.DependencyInjection/)

**A lightweight, fast IoC container for .NET** with singleton, scoped and transient lifetimes, keyed services, open generics, property injection and graph validation. It targets .NET 11, .NET 10 and .NET Standard 2.0, is trimming and Native AOT compatible, and matches or beats [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/) on warm resolves with identical allocations.

Unlike build-once providers the container stays **mutable**: you can keep registering while the application runs, and with read-only mode turned off you can also overwrite and remove registrations. When configuration is final, an opt-in `Freeze()` validates the graph and locks it in for maximum speed. The contracts live in [`Snowberry.DependencyInjection.Abstractions`](https://www.nuget.org/packages/Snowberry.DependencyInjection.Abstractions/), which this package references.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Top Features at a Glance](#top-features-at-a-glance)
- [Service Lifetimes](#service-lifetimes)
- [Registration](#registration)
- [Scopes](#scopes)
- [Injection Attributes](#injection-attributes)
- [Validation](#validation)
- [Freezing](#freezing)
- [Disposal](#disposal)
- [Native AOT and Trimming](#native-aot-and-trimming)
- [How It Works](#how-it-works)
- [Pitfalls](#pitfalls)
- [Related Packages](#related-packages)

## Installation

```bash
dotnet add package Snowberry.DependencyInjection
```

## Quick Start

```cs
using Snowberry.DependencyInjection;
using Snowberry.DependencyInjection.Abstractions.Extensions;

// 1. Create the container (it disposes the instances it creates).
await using var container = new ServiceContainer();

// 2. Register services with a lifetime.
container.RegisterSingleton<IClock, SystemClock>();
container.RegisterScoped<IUnitOfWork, UnitOfWork>();
container.RegisterTransient<OrderService>();

// 3. Optional: validate the graph and lock the configuration.
container.Freeze();

// 4. Resolve inside a scope; scoped services live until the scope is disposed.
using (var scope = container.CreateScope())
{
    var orders = scope.ServiceProvider.GetRequiredService<OrderService>();
    orders.Place("book");
}

public interface IClock { DateTime UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

public interface IUnitOfWork { void Commit(); }
public sealed class UnitOfWork : IUnitOfWork, IDisposable
{
    public void Commit() { }
    public void Dispose() { }
}

public sealed class OrderService(IClock clock, IUnitOfWork unitOfWork)
{
    public void Place(string item)
    {
        Console.WriteLine($"{clock.UtcNow:O}: {item}");
        unitOfWork.Commit();
    }
}
```

## Top Features at a Glance

- **Three lifetimes plus keys.** Singleton, scoped and transient registrations, each optionally keyed with any object, resolved through `GetRequiredService<T>()` or `GetRequiredKeyedService<T>(key)`.
- **Constructor and property injection.** The constructor with the most parameters is used (or the one marked `[PreferredConstructor]`); `[Inject]` properties are filled after construction; `[FromKeyedServices]` picks a keyed dependency.
- **Open generics.** Register `IRepository<>` once and resolve any closed `IRepository<T>`.
- **Fail-fast validation.** `Validate()` / `TryValidate()` report missing dependencies, cycles and unconstructable types without constructing anything.
- **Mutable or frozen.** Keep the container mutable for runtime reconfiguration, or `Freeze()` it to lock registrations and inline whole transient subtrees.
- **Fast and lean.** Warm singleton and scoped resolves allocate nothing; activation starts on reflection and compiles to expression trees on reuse, so startup stays cheap.
- **Safe disposal.** Created instances are disposed in reverse order, synchronously or with `DisposeAsync`; a throwing instance never stops the others from being disposed.
- **Native AOT ready.** Fully annotated for trimming; under Native AOT the reflection path is used and the expression compiler is trimmed away.

## Service Lifetimes

| Lifetime  | Instances                                    | Disposed by                    |
| --------- | -------------------------------------------- | ------------------------------ |
| Singleton | One per container.                           | The container.                 |
| Scoped    | One per scope.                               | The scope that created it.     |
| Transient | A new instance on every resolve.             | The scope that created it.     |

```cs
container.RegisterSingleton<IFoo, Foo>();
container.RegisterScoped<IBar, Bar>();
container.RegisterTransient<IBaz, Baz>();
```

## Registration

**Instances and factories.** Register a pre-built singleton instance (never disposed by the container), or a factory that receives the resolving provider and the service key:

```cs
container.RegisterSingleton<IFoo>(instance: new Foo());
container.RegisterTransient<IBar>((serviceProvider, serviceKey) => new Bar());
```

**Keyed services.** Every `Register*` method takes an optional service key:

```cs
container.RegisterTransient<IFoo, Foo>("primary");

var foo = container.GetRequiredKeyedService<IFoo>("primary");
```

**Open generic types.** Register the generic type definitions through a `ServiceDescriptor`:

```cs
using Snowberry.DependencyInjection.Abstractions.Implementation;

container.Register(ServiceDescriptor.Transient(typeof(IRepository<>), typeof(Repository<>)));

var repository = container.GetRequiredService<IRepository<User>>();
```

**Register only once.** The `TryRegister*` methods return `false` instead of replacing or throwing when the service is already registered:

```cs
bool added = container.TryRegisterSingleton<IFoo, Foo>();
```

**Overwriting and removing.** By default the container is read-only for existing registrations (`ServiceContainerOptions.Default` is `ReadOnly`): registering a service twice or unregistering it throws `ServiceRegistryReadOnlyException`. Clear the flag to allow both:

```cs
var container = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);

container.RegisterTransient<IFoo, Foo>();
container.RegisterSingleton<IFoo, OtherFoo>(); // replaces the registration
container.UnregisterService<IFoo>(serviceKey: null, out bool removed);
```

Instances created before a change are still disposed as usual. Unregistering a singleton disposes its current instance immediately when it implements `IDisposable`, including an instance you supplied.

## Scopes

```cs
container.RegisterScoped<IUnitOfWork, UnitOfWork>();

using (var scope = container.CreateScope())
{
    // Created for this scope and disposed when the scope is disposed.
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
}
```

Resolving a scoped service directly from the container uses the container's own root scope, so the instance lives as long as the container. To make that an error instead, enable scope validation:

```cs
var container = new ServiceContainer(ServiceContainerOptions.Default | ServiceContainerOptions.ValidateScopes);
// Resolving a scoped service from the container now throws ServiceScopeRequiredException.
```

## Injection Attributes

### `InjectAttribute` (`[Inject]`)

Injects a service into a **property** after construction.

- Valid on properties with a setter. Cannot be applied more than once. Inherited.
- Required by default: if the service is not registered, resolution throws `ServiceTypeNotRegistered`. Pass `isRequired: false` to make it optional; an unregistered optional service assigns `null` (or the default value of a value type) to the property.
- Can be combined with `[FromKeyedServices]`.

```cs
[Inject]
public IFoo Foo { get; set; } = null!;

[Inject(isRequired: false)]
public ILogger? OptionalLogger { get; set; }
```

### `FromKeyedServicesAttribute` (`[FromKeyedServices]`)

Selects which keyed service to use for a **property or constructor parameter**.

- Valid on properties and parameters. Cannot be applied more than once. Inherited.
- On a property, combine it with `[Inject]`; on a constructor parameter it works on its own.

```cs
// Property:
[Inject]
[FromKeyedServices("primary")]
public IFoo? PrimaryFoo { get; set; }

// Constructor parameter:
public MyService([FromKeyedServices("primary")] IFoo foo) { }
```

### `PreferredConstructorAttribute` (`[PreferredConstructor]`)

Specifies which **constructor** the container uses. Without it, the public constructor with the most parameters is chosen.

- Valid on constructors only. Cannot be applied more than once. Not inherited.
- Not needed when the type has a single public constructor.

```cs
public class MyService
{
    public MyService() { }

    [PreferredConstructor]
    public MyService(IFoo foo) { }
}
```

Constructor parameters with a non-null default value are optional: when their service is not registered, the default is passed.

## Validation

Check the whole registered graph up front ("fail fast at startup") without constructing any instances. The container stays mutable; run it again after further changes.

```cs
// Throws ServiceValidationException listing every problem.
container.Validate();

// Or collect the problems without throwing.
if (!container.TryValidate(out var errors))
{
    foreach (var error in errors)
        Console.WriteLine($"{error.Kind}: {error.Message}"); // MissingDependency, CircularDependency, NoPublicConstructor
}
```

Factory and instance registrations are treated as opaque, and open generic registrations are checked structurally only. Missing required dependencies and dependency cycles also surface at resolve time (`ServiceTypeNotRegistered`, `CircularDependencyException`).

## Freezing

When configuration is complete, `Freeze()` locks the container for the fastest resolves:

```cs
container.Freeze();                 // validates first; Freeze(validate: false) skips validation
bool locked = container.IsFrozen;

container.RegisterTransient<IBaz, Baz>(); // throws ServiceRegistryReadOnlyException
```

- **Validates by default.** `Freeze()` runs `Validate()` first; on a problem it throws `ServiceValidationException` and the container stays mutable.
- **One-way and idempotent.** Freezing again is a no-op and does not re-validate.
- **Stronger than `ReadOnly`.** `ServiceContainerOptions.ReadOnly` blocks overwriting and removing; freezing blocks every registration change, including new ones.
- **Inlines whole transient subtrees.** A frozen container constructs non-disposable transient dependencies directly inside their parent, which mainly speeds up deep graphs.

| Mode                | Add / remove / overwrite                      | Warm-resolve speed                                              |
| ------------------- | --------------------------------------------- | --------------------------------------------------------------- |
| Mutable (default)   | Add; remove and overwrite when not read-only  | Fast: at or ahead of MS.DI on singleton, scoped and transient   |
| Frozen (`Freeze()`) | No (locked)                                   | Fastest for deep graphs: full transient-subtree inlining        |

## Disposal

Dispose the container (`Dispose()` or `await container.DisposeAsync()`) when you are done, and dispose every scope you create. Each disposes the instances it created, in reverse creation order; instances you registered yourself are not disposed (unless you unregister them, see [Registration](#registration)).

- **Every instance is disposed, even when some throw.** A single failure is rethrown unchanged; several failures are thrown together as an `AggregateException`, in disposal order. This matches `Microsoft.Extensions.DependencyInjection` from .NET 11 on.
- **Async-only services need `DisposeAsync`.** Synchronous `Dispose` reports an instance that only implements `IAsyncDisposable` as an `InvalidOperationException`, after disposing the rest.
- **Disposed means closed.** Resolving from a disposed scope or container throws `ObjectDisposedException`.

## Native AOT and Trimming

The library is annotated for trimming and Native AOT (`IsAotCompatible`), and the registration APIs carry `[DynamicallyAccessedMembers]` so the constructors and properties of registered types are preserved. Under Native AOT activation always runs through `ConstructorInvoker` (reflection) instead of compiled expression trees.

- **Register closed generic types.** Open generic registrations close the implementation with `MakeGenericType` at runtime, which needs dynamic code.
- **Prefer factories for full control.** A factory registration runs no reflection at all.
- **`CreateInstance` is not AOT-safe.** `IServiceProvider.CreateInstance(...)` constructs unregistered types and is annotated `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.

## How It Works

Every service gets a resolver, cached per container. The first resolve builds the resolver graph for the requested type; later resolves are a dictionary lookup plus a delegate call.

```text
GetService<T>()
  |
  v
resolver cache --hit--> invoke resolver --> instance
  | miss
  v
build resolver graph: select constructor, collect [Inject] properties,
capture each dependency's resolver (cycles throw CircularDependencyException)
  |
  v
1st activation: construct through reflection (ConstructorInvoker)
2nd activation: compile an expression tree and swap it in
                (skipped for singletons and under Native AOT)
```

Singletons are baked into constants after their first construction, and scoped instances are read lock-free, so warm singleton and scoped resolves allocate nothing. A registration change discards the cached resolvers, which rebuild on the next resolve; this is what keeps a mutable container correct. See [ARCHITECTURE.md](https://github.com/snowberry-software/DependencyInjection/blob/master/docs/ARCHITECTURE.md) for diagrams of the resolver graph and the mutable to frozen lifecycle, and the [benchmark results](https://github.com/snowberry-software/DependencyInjection/blob/master/src/Benchmarks/Snowberry.DependencyInjection.Benchmarks/results/baseline-net11.md) for numbers against `Microsoft.Extensions.DependencyInjection`.

## Pitfalls

- **Registering a service twice throws by default.** `ServiceContainerOptions.Default` is `ReadOnly`, so overwriting or unregistering throws `ServiceRegistryReadOnlyException`. Clear `ReadOnly` if you reconfigure at runtime, or use `TryRegister*`.
- **Scoped services resolved from the container live as long as the container.** They come from the root scope. Resolve them from `CreateScope()`, and enable `ValidateScopes` to catch mistakes.
- **An explicit `= null` default does not make a constructor parameter optional.** Only a non-null default value does; a parameter declared `IFoo? foo = null` is still required.
- **Optional `[Inject(false)]` properties are overwritten.** When the service is not registered the property is set to `null` (or default), replacing any initializer value.
- **Synchronous `Dispose` cannot dispose async-only services.** Use `await container.DisposeAsync()` (and `DisposeAsync` on scopes) when any service only implements `IAsyncDisposable`.
- **The second resolve of a type pays for compilation.** The first activation is cheap reflection; the second compiles the expression tree once. Warm up hot paths at startup if that one-time cost matters.
- **Open generics need dynamic code.** Under Native AOT, register the closed generic types you resolve.

## Related Packages

| Package | Purpose |
| ------- | ------- |
| [Snowberry.DependencyInjection.Abstractions](https://www.nuget.org/packages/Snowberry.DependencyInjection.Abstractions/) | Interfaces, attributes, `ServiceDescriptor` and the registration and resolution extension methods. |
| [Snowberry.Mediator.DependencyInjection](https://www.nuget.org/packages/Snowberry.Mediator.DependencyInjection/) | Registers [Snowberry.Mediator](https://www.nuget.org/packages/Snowberry.Mediator/) handlers in this container. |
| [Snowberry.Mediator.OpenTelemetry](https://www.nuget.org/packages/Snowberry.Mediator.OpenTelemetry/) | Snowberry.Mediator OpenTelemetry tracing and metrics, wired through this container. |
