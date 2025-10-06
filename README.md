[![License](https://img.shields.io/github/license/snowberry-software/DependencyInjection)](https://github.com/snowberry-software/DependencyInjection/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Snowberry.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/Snowberry.DependencyInjection/)

A lightweight easy to use IoC container for .NET.

# Usage

The `ServiceContainer` should be disposed when no longer in use.

```cs
var serviceContainer = new ServiceContainer();

// Provided instances won't be disposed by the container.
var providedSingletonInstance = new SomeType();
serviceContainer.RegisterSingleton<ISomeType>(instance: providedSingletonInstance);

// The instance created by the container will be disposed by the container.
serviceContainer.RegisterSingleton<ISomeOtherType, SomeOtherType>();
serviceContainer.RegisterTransient<ITransientType, TransientType>();

// Dispose container
serviceContainer.Dispose();

// Or
serviceContainer.DisposeAsync()
```

Existing registered services can be overwritten, if the `ServiceContainer` is not created with the `ServiceContainerOptions.ReadOnly` option.

```cs
var serviceContainer = new ServiceContainer(ServiceContainerOptions.Default & ~ServiceContainerOptions.ReadOnly);

serviceContainer.RegisterTransient<ITransientType, TransientType>();

// This makes the ITransientType service a singleton service with another implementation type.
// The container/scope will still dispose the previously created transient service instances.
serviceContainer.RegisterSingleton<ITransientType, NewTransientType>();

...
```

## Scopes

```cs
var serviceContainer = new ServiceContainer();

serviceContainer.RegisterScoped<IScopedType, ScopedType>();
serviceContainer.RegisterTransient<ITransientType, TransientType>();
serviceContainer.RegisterTransient<ITestService, TestServiceKeyedA>("_KEY0_");

// The current scope in this case would be the container itself, means it will be disposed by the container.
_ = serviceContainer.GetRequiredService<IScopedType>();
_ = serviceContainer.GetRequiredService<ITransientType>();
_ = serviceContainer.GetRequiredKeyedService<ITestService>("_KEY0_");

using(var scope = serviceContainer.CreateScope())
{
    // The instance was created for current scope, the instance will be disposed by the scope.
    _ = scope.ServiceFactory.GetRequiredService<IScopedType>();
    _ = scope.ServiceFactory.GetRequiredService<ITransientType>();
}

// Dispose container
serviceContainer.Dispose();
```

## Open Generic Types

```cs
serviceContainer.Register(typeof(IOpenGenericService<>), typeof(OpenGenericService<>), serviceKey: null, lifetime: ServiceLifetime.Transient, singletonInstance: null);
```

## Attributes

### PreferredConstructorAttribute

The `PreferredConstructorAttribute` attribute can be used to explicitly prefer a constructor. The attribute should be used only once.

## FromKeyedServicesAttribute

The `FromKeyedServicesAttribute` attribute can be used to explicitly use a service from a keyed service collection. 

- The attribute can't be used multiple times.
- The attribute can only be used on properties and parameters and is inherited.

### InjectAttribute

The `InjectAttribute` attribute can be used to explicitly inject a service into a property. 

- The attribute can't be used multiple times. 
- The attribute can only be used on properties and is inherited.

The attribute can also be combined with the `FromKeyedServicesAttribute` attribute.

```cs
[Inject]
[FromKeyedServices("_KEY1_")]
public ITestService? KeyedPropertyInjectTestService { get; set; }
```

## Service lifetime

| Name      | Description                                                                              |
| --------- | ---------------------------------------------------------------------------------------- |
| Singleton | Specifies that a single instance of the service will be created.                         |
| Transient | Specifies that a new instance of the service will be created every time it is requested. |
| Scoped    | Specifies that a new instance of the service will be created for each scope.             |
