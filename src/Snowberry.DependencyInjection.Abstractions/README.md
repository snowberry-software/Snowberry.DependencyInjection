# Snowberry.DependencyInjection.Abstractions

[![NuGet Version](https://img.shields.io/nuget/v/Snowberry.DependencyInjection.Abstractions.svg?logo=nuget)](https://www.nuget.org/packages/Snowberry.DependencyInjection.Abstractions/)

**The contracts of [Snowberry.DependencyInjection](https://www.nuget.org/packages/Snowberry.DependencyInjection/)**: the container, registry, scope and factory interfaces, the injection attributes, `ServiceDescriptor` and `ServiceLifetime`, the exceptions the container throws, and the extension methods used to register and resolve services. It targets .NET 11, .NET 10 and .NET Standard 2.0, is annotated for trimming and Native AOT, and has no runtime dependencies.

Reference it from a library that registers or resolves services without taking a dependency on the container implementation; the application that composes everything references `Snowberry.DependencyInjection`, which brings this package along.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Top Features at a Glance](#top-features-at-a-glance)
- [What Is Inside](#what-is-inside)
- [Extension Methods](#extension-methods)
- [Pitfalls](#pitfalls)
- [Related Packages](#related-packages)

## Installation

```bash
dotnet add package Snowberry.DependencyInjection.Abstractions
```

## Quick Start

```cs
using Snowberry.DependencyInjection;
using Snowberry.DependencyInjection.Abstractions.Extensions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;

// 3. The application, which references Snowberry.DependencyInjection, composes and resolves.
using var container = new ServiceContainer();
container.AddGreeting();

Console.WriteLine(container.GetRequiredService<IGreeter>().Greet("world"));

// 1. A library that references only the abstractions declares its services...
public interface IGreeter { string Greet(string name); }
internal sealed class Greeter : IGreeter { public string Greet(string name) => $"Hello, {name}!"; }

// 2. ...and exposes a registration method over the IServiceRegistry contract.
public static class GreetingRegistration
{
    public static IServiceRegistry AddGreeting(this IServiceRegistry registry)
    {
        registry.RegisterSingleton<IGreeter, Greeter>();
        return registry;
    }
}
```

## Top Features at a Glance

- **Container-agnostic registration.** Write `AddXyz(this IServiceRegistry registry)` methods against the interfaces; any `IServiceRegistry` implementation can consume them.
- **Typed registration helpers.** `RegisterSingleton`, `RegisterScoped` and `RegisterTransient` with implementation types, instances or `ServiceInstanceFactory<T>` factories, each optionally keyed, plus `TryRegister*` variants.
- **Typed resolution helpers.** `GetService<T>`, `GetRequiredService<T>`, `GetKeyedService<T>`, `GetRequiredKeyedService<T>`, `CreateScope` and `CreateInstance<T>` on any `IServiceProvider`.
- **Injection attributes.** `[Inject]`, `[FromKeyedServices]` and `[PreferredConstructor]` mark types for the container without referencing it.
- **Explicit failures.** Dedicated exceptions (`ServiceTypeNotRegistered`, `CircularDependencyException`, `ServiceValidationException`, ...) and a structured `ServiceValidationError` list from validation.
- **Trimming-safe signatures.** Type parameters and `Type` arguments that are activated carry `[DynamicallyAccessedMembers]`, so the trimmer keeps the constructors and properties the container needs.

## What Is Inside

| Export | Kind | Purpose |
| ------ | ---- | ------- |
| `IServiceContainer` | Interface | The full container: registry, descriptor lookup, keyed service provider and disposal. |
| `IServiceRegistry` | Interface | `Register`, `UnregisterService` and `IsServiceRegistered`. |
| `IServiceDescriptorReceiver` | Interface | Looks up registered `IServiceDescriptor`s; enumerable over all of them. |
| `IKeyedServiceProvider` | Interface | `IServiceProvider` plus `GetKeyedService(Type, object?)`. |
| `IServiceScopeFactory` / `IScope` | Interfaces | Create scopes; a scope exposes its provider, scoped-instance cache and disposables. |
| `IServiceFactory` | Interface | Constructs instances of implementation types; the container's activation strategy. |
| `IDisposableContainer` | Interface | Tracks the disposables owned by a scope or container. |
| `IServiceDescriptor` / `ServiceDescriptor` | Interface / class | A registration: service type, implementation type, lifetime, instance or factory. `ServiceDescriptor.Singleton/Scoped/Transient` create one. |
| `IServiceIdentifier` / `IScopeServiceCacheEntry` | Interfaces | A service type plus optional key; a cached scoped instance. |
| `ServiceInstanceFactory` / `ServiceInstanceFactory<T>` | Delegates | Factories that receive the resolving provider and the service key. |
| `ServiceLifetime` | Enum | `Singleton`, `Scoped`, `Transient`. |
| `InjectAttribute`, `FromKeyedServicesAttribute`, `PreferredConstructorAttribute` | Attributes | Property injection, keyed dependency selection, constructor selection. |
| `ServiceTypeNotRegistered`, `CircularDependencyException`, `ServiceRegistryReadOnlyException`, `ServiceScopeRequiredException`, `InvalidServiceImplementationType`, `ServiceValidationException` | Exceptions | The failures the container reports. |
| `ServiceValidationError` / `ServiceValidationErrorKind` | Class / enum | One validation problem: `MissingDependency`, `CircularDependency` or `NoPublicConstructor`. |
| `ServiceRegistryExtensions`, `ServiceProviderExtensions` | Static classes | The registration and resolution helpers below. |
| `ThrowHelper` | Static class | Throw helpers for `IServiceFactory` implementations. |

## Extension Methods

Registration, on `IServiceRegistry` (namespace `Snowberry.DependencyInjection.Abstractions.Extensions`):

| Method | Registers |
| ------ | --------- |
| `RegisterSingleton<T>()`, `RegisterSingleton<T, TImpl>()` | A singleton constructed by the container. |
| `RegisterSingleton<T>(T instance)`, `RegisterSingleton<T, TImpl>(TImpl instance)` | A pre-built singleton instance (not disposed by the container). |
| `RegisterSingleton<T>(ServiceInstanceFactory<T>)`, `RegisterSingleton<T, TImpl>(ServiceInstanceFactory<TImpl>)` | A singleton created by a factory. |
| `RegisterScoped<...>(...)`, `RegisterTransient<...>(...)` | The same type and factory forms for the other lifetimes. |
| `TryRegister(descriptor)`, `TryRegisterSingleton/Scoped/Transient<...>(...)` | Like the above, but returns `false` instead of registering when the service is already registered. |

Every registration method takes an optional `serviceKey`.

Resolution, on `IServiceProvider` / `IKeyedServiceProvider`:

| Method | Returns |
| ------ | ------- |
| `GetService<T>()`, `GetKeyedService<T>(key)` | The service, or `default` when it is not registered. |
| `GetRequiredService<T>()`, `GetRequiredService(Type)` | The service; throws `ServiceTypeNotRegistered` when it is not registered. |
| `GetRequiredKeyedService<T>(key)`, `GetRequiredKeyedService(Type, key)` | The keyed service; throws `ServiceTypeNotRegistered` when it is not registered. |
| `CreateScope()` | A new `IScope` from the provider's `IServiceScopeFactory`. |
| `CreateInstance<T>()`, `CreateInstance(Type)` | A new instance of an unregistered type, with its dependencies injected. |

## Pitfalls

- **The abstractions do not contain a container.** `ServiceContainer` and `ServiceContainerOptions` live in `Snowberry.DependencyInjection`; reference that package in the application that composes the services.
- **A wrong implementation type fails at resolve time.** `GetService<T>` and the other typed helpers throw `InvalidCastException` when the registered instance is not a `T`.
- **`CreateInstance` is not trimming or AOT safe.** It is annotated `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` because it can close generic types at runtime; prefer registering the type.
- **Async disposal is .NET only.** On .NET Standard 2.0 the interfaces do not include `IAsyncDisposable` and `RegisterDisposable(IAsyncDisposable)`.

## Related Packages

| Package | Purpose |
| ------- | ------- |
| [Snowberry.DependencyInjection](https://www.nuget.org/packages/Snowberry.DependencyInjection/) | The container that implements these contracts. |
| [Snowberry.Mediator.DependencyInjection](https://www.nuget.org/packages/Snowberry.Mediator.DependencyInjection/) | Registers [Snowberry.Mediator](https://www.nuget.org/packages/Snowberry.Mediator/) handlers in the Snowberry container. |
