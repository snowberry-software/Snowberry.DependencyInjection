using Snowberry.DependencyInjection.Abstractions.Attributes;

namespace Snowberry.DependencyInjection.Benchmarks.Fixtures;

// Benchmark-local fixtures. Intentionally NOT the xUnit TestModels (those all implement IDisposable,
// which would fold disposable-registration cost into every measurement). Non-disposable variants are
// used for pure resolution; the explicit DisposableService is used only by the disposable-path benches.

// --- 0-dependency ---
public interface IServiceA;
public sealed class ServiceA : IServiceA;

// --- 1 dependency ---
public interface IServiceB;
public sealed class ServiceB(IServiceA a) : IServiceB
{
    public IServiceA A { get; } = a;
}

// --- deep linear chain (5 levels) ---
public interface IChain1;
public interface IChain2;
public interface IChain3;
public interface IChain4;
public interface IChain5;

public sealed class Chain1 : IChain1;
public sealed class Chain2(IChain1 inner) : IChain2 { public IChain1 Inner { get; } = inner; }
public sealed class Chain3(IChain2 inner) : IChain3 { public IChain2 Inner { get; } = inner; }
public sealed class Chain4(IChain3 inner) : IChain4 { public IChain3 Inner { get; } = inner; }
public sealed class Chain5(IChain4 inner) : IChain5 { public IChain4 Inner { get; } = inner; }

// --- wide graph (8 distinct dependencies) ---
public interface IDep1;
public interface IDep2;
public interface IDep3;
public interface IDep4;
public interface IDep5;
public interface IDep6;
public interface IDep7;
public interface IDep8;

public sealed class Dep1 : IDep1;
public sealed class Dep2 : IDep2;
public sealed class Dep3 : IDep3;
public sealed class Dep4 : IDep4;
public sealed class Dep5 : IDep5;
public sealed class Dep6 : IDep6;
public sealed class Dep7 : IDep7;
public sealed class Dep8 : IDep8;

public interface IWideRoot;
public sealed class WideRoot(IDep1 d1, IDep2 d2, IDep3 d3, IDep4 d4, IDep5 d5, IDep6 d6, IDep7 d7, IDep8 d8) : IWideRoot
{
    public IDep1 D1 { get; } = d1;
    public IDep2 D2 { get; } = d2;
    public IDep3 D3 { get; } = d3;
    public IDep4 D4 { get; } = d4;
    public IDep5 D5 { get; } = d5;
    public IDep6 D6 { get; } = d6;
    public IDep7 D7 { get; } = d7;
    public IDep8 D8 { get; } = d8;
}

// --- property injection ---
public interface IPropTarget;
public sealed class PropTarget : IPropTarget
{
    [Inject]
    public IServiceA? A { get; set; }
}

// --- hybrid: ctor + property injection (for CreateInstance) ---
public interface IHybrid;
public sealed class Hybrid(IServiceA a) : IHybrid
{
    public IServiceA A { get; } = a;

    [Inject]
    public IServiceB? B { get; set; }
}

// --- unregistered activation target (ctor injection only) ---
public sealed class ActivatedWithCtor(IServiceA a)
{
    public IServiceA A { get; } = a;
}

// --- open generic ---
public sealed class BenchEntity;
public interface IRepository<T>;
public sealed class Repository<T> : IRepository<T>;

// --- keyed ---
public interface IKeyed;
public sealed class KeyedA : IKeyed;
public sealed class KeyedB : IKeyed;

// --- deliberately disposable (disposal-path benches only) ---
public interface IDisposableService;
public sealed class DisposableService : IDisposableService, IDisposable
{
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

/// <summary>
/// Marker service/impl type pairs for registration-throughput benchmarks (registered via the
/// non-generic <c>ServiceDescriptor</c> factory so any lifetime / count N can be driven by a loop).
/// </summary>
public static class Markers
{
    public static readonly Type[] ServiceTypes =
    [
        typeof(IMarker01), typeof(IMarker02), typeof(IMarker03), typeof(IMarker04),
        typeof(IMarker05), typeof(IMarker06), typeof(IMarker07), typeof(IMarker08),
        typeof(IMarker09), typeof(IMarker10), typeof(IMarker11), typeof(IMarker12),
        typeof(IMarker13), typeof(IMarker14), typeof(IMarker15), typeof(IMarker16),
    ];

    public static readonly Type[] ImplTypes =
    [
        typeof(Marker01), typeof(Marker02), typeof(Marker03), typeof(Marker04),
        typeof(Marker05), typeof(Marker06), typeof(Marker07), typeof(Marker08),
        typeof(Marker09), typeof(Marker10), typeof(Marker11), typeof(Marker12),
        typeof(Marker13), typeof(Marker14), typeof(Marker15), typeof(Marker16),
    ];

    public const int Count = 16;
}

public interface IMarker01; public sealed class Marker01 : IMarker01;
public interface IMarker02; public sealed class Marker02 : IMarker02;
public interface IMarker03; public sealed class Marker03 : IMarker03;
public interface IMarker04; public sealed class Marker04 : IMarker04;
public interface IMarker05; public sealed class Marker05 : IMarker05;
public interface IMarker06; public sealed class Marker06 : IMarker06;
public interface IMarker07; public sealed class Marker07 : IMarker07;
public interface IMarker08; public sealed class Marker08 : IMarker08;
public interface IMarker09; public sealed class Marker09 : IMarker09;
public interface IMarker10; public sealed class Marker10 : IMarker10;
public interface IMarker11; public sealed class Marker11 : IMarker11;
public interface IMarker12; public sealed class Marker12 : IMarker12;
public interface IMarker13; public sealed class Marker13 : IMarker13;
public interface IMarker14; public sealed class Marker14 : IMarker14;
public interface IMarker15; public sealed class Marker15 : IMarker15;
public interface IMarker16; public sealed class Marker16 : IMarker16;
