using System.Diagnostics.CodeAnalysis;
using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Lookup;

namespace Snowberry.DependencyInjection;

public partial class ServiceContainer
{
    /// <summary>
    /// Validates the registered service graph, throwing <see cref="ServiceValidationException"/> when any
    /// problems are found (missing required dependencies, circular dependencies, or implementations that cannot
    /// be constructed). No service instances are constructed. The container remains mutable; call this again
    /// after further registration changes.
    /// </summary>
    /// <exception cref="ServiceValidationException">The service graph contains one or more validation problems.</exception>
    public void Validate()
    {
        if (!TryValidate(out var errors))
            throw new ServiceValidationException(errors);
    }

    /// <summary>
    /// Locks the container into an immutable state. This is a one-way, idempotent operation: after freezing,
    /// registration changes throw <see cref="ServiceRegistryReadOnlyException"/>. When <paramref name="validate"/>
    /// is <see langword="true"/> (the default), <see cref="Validate"/> is run before the container is frozen.
    /// </summary>
    /// <param name="validate">When <see langword="true"/> (the default), runs <see cref="Validate"/> before freezing.</param>
    /// <exception cref="ServiceValidationException"><paramref name="validate"/> is <see langword="true"/> and the service graph contains validation problems.</exception>
    public void Freeze(bool validate = true)
    {
        if (_frozen)
            return;

        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        if (validate)
            Validate();

        lock (_lock)
        {
            if (_frozen)
                return;

            // Drop any resolvers built in mutable mode so the next resolve rebuilds them through the frozen
            // (inlining) path. Safe: registrations are now locked.
            _frozen = true;
            InvalidateResolverCaches();
        }
    }

    /// <summary>
    /// Validates the registered service graph without throwing, collecting all problems into
    /// <paramref name="errors"/>. Open-generic registrations are validated structurally only; factory-backed and
    /// instance-backed registrations are treated as opaque. No service instances are constructed.
    /// </summary>
    /// <param name="errors">When this method returns, contains every validation problem found, or an empty list when the graph is valid.</param>
    /// <returns><see langword="true"/> when no validation problems were found; otherwise, <see langword="false"/>.</returns>
    public bool TryValidate(out IReadOnlyList<ServiceValidationError> errors)
    {
        DisposeThrowHelper.ThrowIfDisposed(_isDisposed, this);

        var collected = new List<ServiceValidationError>();

        KeyValuePair<ServiceIdentifier, IServiceDescriptor>[] snapshot;
        lock (_lock)
        {
            snapshot = _serviceDescriptorMapping.ToArray();
        }

        var validatedOk = new HashSet<ServiceIdentifier>(ServiceIdentifierComparer.s_Instance);
        var path = new List<ServiceIdentifier>();

        foreach (var entry in snapshot)
            ValidateNode(entry.Key, entry.Value, path, validatedOk, collected);

        errors = collected;
        return collected.Count == 0;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055:UnrecognizedReflectionPattern", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The closed implementation type carries the same DynamicallyAccessedMembers requirements as the descriptor's annotated ImplementationType.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generic type instantiation is supported through proper service registration. Users must register closed generic types for AOT scenarios.")]
    private void ValidateNode(ServiceIdentifier serviceIdentifier, IServiceDescriptor serviceDescriptor, List<ServiceIdentifier> path, HashSet<ServiceIdentifier> validatedOk, List<ServiceValidationError> errors)
    {
        // Open-generic registration: cannot be closed without type arguments → structural-only (skip).
        if (serviceIdentifier.ServiceType.IsGenericTypeDefinition)
            return;

        // Factory- or instance-backed: opaque/provided → no constructable dependency graph to walk.
        if (serviceDescriptor.InstanceFactory != null || serviceDescriptor.SingletonInstance != null)
            return;

        // Only the default factory can introspect construct-time dependencies.
        if (ServiceFactory is not DefaultServiceFactory defaultFactory)
            return;

        if (validatedOk.Contains(serviceIdentifier))
            return;

        if (path.Contains(serviceIdentifier))
        {
            errors.Add(new ServiceValidationError(
                ServiceValidationErrorKind.CircularDependency,
                serviceIdentifier.ServiceType,
                $"Circular dependency detected: {FormatPath(path, serviceIdentifier.ServiceType)}."));
            return;
        }

        // Close an open-generic implementation against the closed service type.
        var implementationType = serviceDescriptor.ImplementationType;
        var closedImplementationType = implementationType.IsGenericTypeDefinition
            ? implementationType.MakeGenericType(serviceIdentifier.ServiceType.GenericTypeArguments)
            : implementationType;

        if (closedImplementationType.IsInterface || closedImplementationType.IsAbstract)
        {
            errors.Add(new ServiceValidationError(
                ServiceValidationErrorKind.NoPublicConstructor,
                serviceIdentifier.ServiceType,
                $"'{closedImplementationType.FullName}' cannot be constructed (abstract class or interface) and has no factory or instance."));
            return;
        }

        if (!defaultFactory.TryGetDependencies(closedImplementationType, out var dependencies))
        {
            errors.Add(new ServiceValidationError(
                ServiceValidationErrorKind.NoPublicConstructor,
                serviceIdentifier.ServiceType,
                $"'{closedImplementationType.FullName}' has no usable public constructor."));
            return;
        }

        path.Add(serviceIdentifier);
        try
        {
            for (int i = 0; i < dependencies.Count; i++)
            {
                var dependency = dependencies[i];

                // Optional dependencies that are unregistered are NOT errors (they resolve to null/default).
                if (!dependency.Required)
                    continue;

                // Built-in services (null key only) are always available.
                if (dependency.ServiceKey is null && IsBuiltInService(dependency.DependencyType))
                    continue;

                var dependencyIdentifier = new ServiceIdentifier(dependency.DependencyType, dependency.ServiceKey);
                var dependencyDescriptor = GetOptionalServiceDescriptor(dependencyIdentifier);
                if (dependencyDescriptor == null)
                {
                    string member = dependency.IsProperty
                        ? $"property '{dependency.MemberName}'"
                        : $"constructor parameter '{dependency.MemberName}'";

                    errors.Add(new ServiceValidationError(
                        ServiceValidationErrorKind.MissingDependency,
                        serviceIdentifier.ServiceType,
                        $"'{serviceIdentifier.ServiceType.FullName}' requires '{dependency.DependencyType.FullName}' ({member}) which is not registered.",
                        dependency.DependencyType));
                    continue;
                }

                ValidateNode(dependencyIdentifier, dependencyDescriptor, path, validatedOk, errors);
            }
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
        }

        validatedOk.Add(serviceIdentifier);
    }

    private static string FormatPath(List<ServiceIdentifier> path, Type closing)
    {
        var names = new List<string?>(path.Count + 1);
        for (int i = 0; i < path.Count; i++)
            names.Add(path[i].ServiceType.FullName);

        names.Add(closing.FullName);
        return string.Join(" -> ", names);
    }
}
