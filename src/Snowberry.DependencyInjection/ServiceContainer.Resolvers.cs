using Snowberry.DependencyInjection.Abstractions.Exceptions;
using Snowberry.DependencyInjection.Abstractions.Interfaces;
using Snowberry.DependencyInjection.Helper;
using Snowberry.DependencyInjection.Implementation;
using Snowberry.DependencyInjection.Lookup;

namespace Snowberry.DependencyInjection;

public partial class ServiceContainer
{
    /// <summary>
    /// How a resolver decides whether a constructed instance is tracked for disposal.
    /// </summary>
    private enum DisposalTracking : byte
    {
        /// <summary>The runtime type is not known when the resolver is built (custom factory); test each instance.</summary>
        Check,

        /// <summary>The constructed type is known to be disposable; always track.</summary>
        Always,

        /// <summary>The constructed type is known not to be disposable; never track.</summary>
        Never
    }

    /// <summary>
    /// A cached resolver: applies a service lifetime to the instances produced by <see cref="Construct"/>.
    /// The cache and the compiled parents hold a delegate bound to <see cref="Resolve"/>.
    /// </summary>
    private abstract class ResolverNode
    {
        /// <summary>
        /// The delegate that constructs a new instance. A <see cref="TieredConstruct"/> replaces it with the compiled
        /// delegate; every holder of the <see cref="Resolve"/> delegate observes the replacement.
        /// </summary>
        internal Func<DefaultServiceScopeProvider, object> Construct = null!;

        /// <summary>How constructed instances are tracked for disposal.</summary>
        internal DisposalTracking Tracking;

        /// <summary>
        /// Resolves the service for <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope the service is resolved in.</param>
        /// <returns>The resolved instance.</returns>
        internal abstract object? Resolve(DefaultServiceScopeProvider scope);

        /// <summary>
        /// Determines whether <paramref name="instance"/> is tracked for disposal under <see cref="Tracking"/>.
        /// </summary>
        /// <param name="instance">The constructed instance.</param>
        /// <returns><see langword="true"/> if the instance must be tracked; otherwise, <see langword="false"/>.</returns>
        protected bool MustTrack(object instance)
        {
            var tracking = Tracking;
            return tracking == DisposalTracking.Always || (tracking == DisposalTracking.Check && instance.IsDisposable());
        }
    }

    /// <summary>
    /// Resolves a transient service: constructs a new instance on every call.
    /// </summary>
    private sealed class TransientResolverNode : ResolverNode
    {
        /// <inheritdoc/>
        internal override object? Resolve(DefaultServiceScopeProvider scope)
        {
            object instance = Construct(scope);

            if (Tracking != DisposalTracking.Never && MustTrack(instance))
                scope.TrackNewDisposable(instance);

            return instance;
        }
    }

    /// <summary>
    /// Resolves a scoped service: one instance per scope.
    /// </summary>
    /// <param name="serviceIdentifier">The identifier the instance is cached under in each scope.</param>
    /// <param name="validateScope"><see langword="true"/> to reject resolution from the container's root scope (<see cref="ServiceContainerOptions.ValidateScopes"/>).</param>
    private sealed class ScopedResolverNode(ServiceIdentifier serviceIdentifier, bool validateScope) : ResolverNode
    {
        private readonly ServiceIdentifier _serviceIdentifier = serviceIdentifier;
        private readonly bool _validateScope = validateScope;

        /// <inheritdoc/>
        /// <exception cref="ServiceScopeRequiredException">Scope validation is enabled and <paramref name="scope"/> is the container's root scope.</exception>
        internal override object? Resolve(DefaultServiceScopeProvider scope)
        {
            // The validation lives in the resolver so it fires on the warm path AND for children reached via a
            // captured resolver (which never re-enter the GetKeyedService entry check).
            if (_validateScope && scope.IsGlobalScope)
                throw new ServiceScopeRequiredException(_serviceIdentifier.ServiceType);

            if (scope.TryGetScopedInstance(_serviceIdentifier, out object? existing))
                return existing;

            // Construct OUTSIDE any lock (deadlock-free; see TryAddScopedInstance).
            object created = Construct(scope);

            bool won = scope.TryAddScopedInstance(_serviceIdentifier, created, out object? raced);

            // Track whether we won or lost so a disposable loser is disposed at scope teardown, not leaked.
            if (Tracking != DisposalTracking.Never && MustTrack(created))
                scope.TrackNewDisposable(created);

            return won ? created : raced!;
        }
    }

    /// <summary>
    /// Resolves a singleton service: constructs it once in the root scope, then republishes a constant resolver.
    /// </summary>
    /// <param name="container">The container whose root scope owns the instance.</param>
    /// <param name="serviceDescriptor">The descriptor that stores the constructed instance.</param>
    /// <param name="rebake">Publishes the constant resolver into the resolver cache the node was built for.</param>
    private sealed class SingletonResolverNode(ServiceContainer container, IServiceDescriptor serviceDescriptor, Action<Func<DefaultServiceScopeProvider, object?>> rebake) : ResolverNode
    {
        private readonly ServiceContainer _container = container;
        private readonly IServiceDescriptor _serviceDescriptor = serviceDescriptor;
        private readonly Action<Func<DefaultServiceScopeProvider, object?>> _rebake = rebake;

        /// <inheritdoc/>
        internal override object? Resolve(DefaultServiceScopeProvider scope)
        {
            // The singleton subtree is rooted at RootScope: construct/track use RootScope, never the caller's
            // scope (the node threads RootScope to its children).
            if (_serviceDescriptor.SingletonInstance == null)
            {
                lock (_container._lock)
                {
                    if (_serviceDescriptor.SingletonInstance == null)
                    {
                        var rootScope = _container.RootScope;
                        object created = Construct(rootScope);
                        _serviceDescriptor.SingletonInstance = created;

                        if (MustTrack(created))
                            rootScope.TrackNewDisposable(created);
                    }
                }
            }

            object instance = _serviceDescriptor.SingletonInstance!;

            // Constant-bake: warm resolves now return the instance directly (no descriptor virtual read). Written
            // into the captured generation; if a mutation already swapped the cache this lands in the abandoned
            // generation and is harmless.
            _rebake(_ => instance);
            return instance;
        }
    }

    /// <summary>
    /// The construct delegate of a node during its interpreted tier: serves the first invocations through the
    /// reflection-based delegate, then compiles the node once and installs the compiled delegate on its owner.
    /// </summary>
    /// <param name="owner">The resolver node whose <see cref="ResolverNode.Construct"/> receives the compiled delegate.</param>
    /// <param name="interpreted">The reflection-based construct delegate used until the node is compiled.</param>
    /// <param name="compile">Compiles the construct delegate.</param>
    private sealed class TieredConstruct(ResolverNode owner, Func<DefaultServiceScopeProvider, object> interpreted, Func<Func<DefaultServiceScopeProvider, object>> compile)
    {
        private readonly ResolverNode _owner = owner;
        private readonly Func<DefaultServiceScopeProvider, object> _interpreted = interpreted;
        private readonly Func<Func<DefaultServiceScopeProvider, object>> _compile = compile;
        private int _invocationsUntilCompile = RuntimeCompilation.CompileAfterInvocations;

        /// <summary>
        /// Constructs an instance for <paramref name="scope"/>, compiling the node on the configured invocation.
        /// </summary>
        /// <param name="scope">The scope the dependencies are resolved in.</param>
        /// <returns>The constructed instance.</returns>
        internal object Invoke(DefaultServiceScopeProvider scope)
        {
            // Exactly one caller observes zero, so the node is compiled once even under contention. Concurrent
            // callers keep using the interpreted delegate until the owner's Construct field is replaced.
            if (Interlocked.Decrement(ref _invocationsUntilCompile) == 0)
            {
                var compiled = _compile();
                _owner.Construct = compiled;
                return compiled(scope);
            }

            return _interpreted(scope);
        }
    }
}
