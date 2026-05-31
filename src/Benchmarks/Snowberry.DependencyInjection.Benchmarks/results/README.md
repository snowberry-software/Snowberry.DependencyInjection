# Benchmark results

Curated, committed BenchmarkDotNet results for `Snowberry.DependencyInjection`. The raw
`BenchmarkDotNet.Artifacts/` output is git-ignored; the markdown snapshot here is the reference baseline.

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

## Reading the numbers

Absolute timings depend on the machine and runtime, so treat the committed baseline as relative rather than
absolute: the meaningful figures are the same-run Snowberry versus Microsoft.Extensions.DependencyInjection
pairs and the allocated bytes. Re-run on your own hardware for local numbers. Each committed report keeps the
full environment header that BenchmarkDotNet records (CPU, OS, runtime, BenchmarkDotNet version, job), so the
capture conditions stay with the data instead of being duplicated here.

The library multi-targets several frameworks while the benchmark host runs on the latest one only, so the
lock-primitive differences between target frameworks are not exercised by these benchmarks.

## Caveat: cold/compile costs

The constructor-metadata and compiled-resolver caches are **process-static**, so expression compilation is paid
only on the first resolve of each type per process. `Cold_FirstResolve_WideGraph` therefore tracks per-container
first-resolve overhead, not steady-state cost.

## Files

- `baseline-net10.md` is the current authoritative baseline: the full suite under the default job, Snowberry
  (mutable and frozen) versus `Microsoft.Extensions.DependencyInjection`, captured as same-run pairs. Mutable
  mode beats MS.DI on singleton and scoped and is at parity on the simplest transient; frozen mode reaches
  parity on the 0-dep and 1-dep cases and collapses deep and wide graphs. Allocation parity is preserved across
  all comparable cases.
