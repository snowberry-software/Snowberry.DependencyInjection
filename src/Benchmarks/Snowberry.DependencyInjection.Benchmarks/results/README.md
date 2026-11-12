# Benchmark results

Curated, committed BenchmarkDotNet results for `Snowberry.DependencyInjection`. The raw
`BenchmarkDotNet.Artifacts/` output is git-ignored; the markdown snapshot here is the reference baseline.

- [How to run](#how-to-run)
- [What is measured](#what-is-measured)
- [Reading the numbers](#reading-the-numbers)
- [Caveat: cold/compile costs](#caveat-coldcompile-costs)
- [Known issues](#known-issues)
- [Files](#files)

## How to run

```pwsh
# Snowberry only (default)
dotnet run -c Release --project src/Benchmarks/Snowberry.DependencyInjection.Benchmarks -- --filter '*'

# include the opt-in Microsoft.Extensions.DependencyInjection comparison (env var, not /p:, because it must
# propagate to BenchmarkDotNet's child build)
$env:BenchCompare='true'
dotnet run -c Release --project src/Benchmarks/Snowberry.DependencyInjection.Benchmarks -- --filter '*'
Remove-Item Env:\BenchCompare
```

Filter to a subset with `-- --filter *Transient* *Scoped*`. Add `-- --job short` for a fast, lower-fidelity
pass (fine for tracking large deltas; re-run with the default job for publishable figures).

## What is measured

| Class | Measures | MS.DI pair |
| ----- | -------- | ---------- |
| `SingletonResolutionBenchmarks` | Warm singleton resolve. | Yes |
| `ScopedResolutionBenchmarks` | Warm scoped resolve inside one scope. | Yes |
| `TransientResolutionBenchmarks` | Transient resolve: 0 and 1 dependency, deep chain, wide graph, property injection. | 0/1 dependency |
| `FrozenResolutionBenchmarks` | The same transient shapes after `Freeze()`. | 0/1 dependency |
| `KeyedResolutionBenchmarks` | Keyed singleton and transient resolve. | No |
| `OpenGenericResolutionBenchmarks` | Cached open-generic resolve. | No |
| `CreateInstanceBenchmarks` | `CreateInstance` on unregistered types (constructor and property injection). | No |
| `ContainerConstructionBenchmarks` | Constructing an empty container, and one with a few registrations. | No |
| `RegistrationBenchmarks` | Registering N services per lifetime. | No |
| `ScopeLifecycleBenchmarks` | Create, optionally resolve, and dispose a scope. | No |
| `WideScopeBenchmarks` | A scope that resolves 16 scoped services. | No |
| `ParallelScopedResolutionBenchmarks` | Scope create and scoped resolve on 1, 4 and 8 threads. | No |
| `DisposableTransientResolutionBenchmarks` | Resolving N disposable transients into a fresh container (includes compilation). | No |
| `DisposalBenchmarks` | Disposing a container that tracks N disposables. | No |
| `ColdResolveBenchmarks` | First resolve of a wide graph in a fresh container. | No |

## Reading the numbers

Absolute timings depend on the machine and runtime, so treat the committed baseline as relative rather than
absolute: the meaningful figures are the same-run Snowberry versus Microsoft.Extensions.DependencyInjection
pairs and the allocated bytes. Re-run on your own hardware for local numbers. Each committed report keeps the
full environment header that BenchmarkDotNet records (CPU, OS, runtime, BenchmarkDotNet version, job), so the
capture conditions stay with the data instead of being duplicated here.

The library targets net11.0, net10.0 and netstandard2.0 while the benchmark host runs on net11.0 only, so the
netstandard2.0 build (`Monitor` locks instead of `System.Threading.Lock`, no `IAsyncDisposable`) is not
exercised by these benchmarks.

## Caveat: cold/compile costs

Activation is tiered: a node's first activation runs through reflection and its second compiles an expression
tree. The constructor metadata cache is process-static, but compiled resolvers belong to a container, so a fresh
container compiles its own. `Cold_FirstResolve_WideGraph` therefore measures the interpreted first resolve,
while benchmarks that create a container per iteration and resolve more than once (for example
`Resolve_N_DisposableTransients`) include one compilation per node.

## Known issues

- **BenchmarkDotNet 0.16 is required for net11.0.** The stable 0.15.8 release fails with `GetRuntimeVersion not
  implemented for NotRecognized`; the project references `0.16.0-preview.2`.
- **Project names must be unique under the search root.** BenchmarkDotNet locates the benchmark project by file
  name; two copies of this project under the same root (for example two checkouts in `%TEMP%`) fail with
  `Found more than one matching project file`. Rename one copy's `.csproj` when comparing builds side by side.
- **Other benchmark runs skew results.** Run on an otherwise idle machine; concurrent suites from other
  repositories change timings noticeably.

## Files

- `baseline-net11.md` is the current authoritative baseline: the full suite on .NET 11 under the default job,
  Snowberry (mutable and frozen) versus `Microsoft.Extensions.DependencyInjection` 11.0.0, plus the before/after
  comparison for 6.0 (tiered activation, cheaper scopes and keyed lookups). Snowberry is at or ahead of MS.DI on
  every comparable case with identical allocations.
- `baseline-net10.md` is the previous baseline (5.x on .NET 10), kept for history.
