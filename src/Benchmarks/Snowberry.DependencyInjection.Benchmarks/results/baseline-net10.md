# Baseline: Snowberry vs Microsoft.Extensions.DependencyInjection (net10)

Full suite, default job, same-run Snowberry/MS.DI pairs. Full environment is in each report header under
`BenchmarkDotNet.Artifacts/`. Timings on this machine vary run-to-run by 20 to 30 percent, so treat the
allocated bytes and the same-run pairs as the signal, not absolute nanoseconds.

## Warm resolution vs MS.DI

| Method            | Snowberry | MS.DI    | Alloc (both) |
|------------------ |----------:|---------:|-------------:|
| Singleton         | 6.5 ns    | 6.6 ns   | 0 B          |
| Scoped            | 18.8 ns   | 23.1 ns  | 0 B          |
| Transient 0-dep   | 10.0 ns   | 8.9 ns   | 24 B         |
| Transient 1-dep   | 13.1 ns   | 12.2 ns  | 48 B         |

Snowberry wins on singleton and scoped; transient is at parity. Allocation matches MS.DI exactly.

Frozen mode (`Freeze()`): 0-dep 9.9 ns, 1-dep 15.0 ns, deep-5 22.6 ns, wide-8 35.5 ns.

## Other (Snowberry only)

| Method                       | Mean      | Alloc  |
|----------------------------- |----------:|-------:|
| Transient deep-5 / wide-8    | 36.7 / 35.1 ns | 120 / 272 B |
| Property injection           | 22.5 ns   | 48 B   |
| Keyed singleton / transient  | 30.3 / 31.7 ns | 0 / 24 B |
| Open-generic (cached)        | 9.8 ns    | 24 B   |
| Scope create+resolve+dispose | 108.7 ns  | 472 B  |
| Construct empty container    | 169 ns    | 1.79 KB |
| Cold first-resolve (wide)    | 179 μs    | 9.25 KB |

## Recent optimizations

- **One-level transient inlining (mutable mode):** a node constructs a simple-transient child directly instead
  of through its resolver delegate. Same-session before/after: 1-dep 18.96 to 10.09 ns, wide-8 93 to 28 ns,
  deep-5 53 to 30 ns; cold wide-graph allocation 50 KB to 9 KB. Dynamic add/remove and allocation parity intact.
- **Right-sized dictionaries (`concurrencyLevel: 1`):** empty-container allocation 5.05 KB to 1.79 KB, and
  construct-and-register-few 14.77 KB to 5.0 KB.
