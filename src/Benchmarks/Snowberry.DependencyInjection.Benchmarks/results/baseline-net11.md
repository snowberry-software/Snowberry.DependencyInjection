# Baseline: Snowberry vs Microsoft.Extensions.DependencyInjection (net11)

Full suite, default job, BenchmarkDotNet 0.16.0-preview.2 (0.15.8 does not recognize net11.0), with
`BenchCompare=true` so MS.DI 11.0.0 runs in the same process. Captured on 2026-11-11:

```
Windows 11 25H2, AMD Ryzen 9 7950X3D 4.20GHz, 16 physical / 32 logical cores
.NET SDK 11.0.100, .NET 11.0.0, X64 RyuJIT x86-64-v4
```

"Before" is the 5.x resolver (eager expression compilation, per-instance disposal checks, copy-on-write
dictionary for scoped instances) built for net11.0; "after" is 6.0 (tiered activation, build-time disposal
tracking, small-array scoped cache, lazy scope internals). Both ran back to back on an otherwise idle machine.
Timings vary run to run by roughly 5 to 10 percent; treat the allocated bytes and large deltas as the signal.

## Warm resolution vs MS.DI 11

| Method                       | Snowberry | MS.DI    | Alloc (both) |
|----------------------------- |----------:|---------:|-------------:|
| Singleton                    | 5.3 ns    | 5.7 ns   | 0 B          |
| Scoped (cached in scope)     | 11.4 ns   | 18.8 ns  | 0 B          |
| Transient 0-dep              | 6.8 ns    | 7.0 ns   | 24 B         |
| Transient 1-dep              | 9.0 ns    | 9.6 ns   | 48 B         |
| Frozen transient 0-dep       | 7.2 ns    | 7.0 ns   | 24 B         |
| Frozen transient 1-dep       | 9.5 ns    | 9.7 ns   | 48 B         |

Snowberry is at or ahead of MS.DI on every comparable case, with identical allocations.

## Before / after (6.0)

| Benchmark                                  | Before    | After     | Change | Alloc before → after |
|------------------------------------------- |----------:|----------:|-------:|---------------------:|
| Cold first resolve, wide graph             | 154.1 μs  | 2.95 μs   | -98%   | 9,608 → 6,208 B      |
| Create scope + dispose (empty)             | 48.9 ns   | 11.4 ns   | -77%   | 184 → 72 B           |
| Create scope + resolve scoped + dispose    | 89.6 ns   | 43.8 ns   | -51%   | 472 → 208 B          |
| Create scope + resolve 16 scoped + dispose | 1.57 μs   | 906 ns    | -42%   | 10,000 → 2,928 B     |
| Parallel scopes (1 / 4 / 8 threads)       | 27.8 / 40.0 / 49.6 μs | 16.2 / 26.6 / 30.0 μs | -33 to -42% | allocations -56% |
| Keyed singleton                            | 23.4 ns   | 10.0 ns   | -57%   | 0 B                  |
| Keyed transient                            | 24.4 ns   | 11.8 ns   | -52%   | 24 B                 |
| Scoped (cached in scope)                   | 16.0 ns   | 11.4 ns   | -29%   | 0 B                  |
| CreateInstance (ctor injection)            | 49.8 ns   | 34.0 ns   | -32%   | 48 B                 |
| CreateInstance (ctor + property)           | 62.2 ns   | 44.5 ns   | -29%   | 104 B                |
| Transient property injection               | 16.5 ns   | 12.1 ns   | -27%   | 48 B                 |
| Transient deep chain (5)                   | 28.6 ns   | 23.1 ns   | -19%   | 120 B                |
| Construct empty container                  | 129.4 ns  | 104.7 ns  | -19%   | 1,832 → 1,680 B      |
| Dispose 100 / 1000 disposables             | 473 ns / 1.24 μs | 436 ns / 1.53 μs | -8% / +24% | 0 B |

Unchanged within noise: singleton, transient 0/1-dep and wide, frozen mode, open generics, registration.

`Dispose 1000` is measured with `IterationSetup` (one invocation per iteration, ~100 ns timer granularity); the
difference is about 0.3 ns per instance and comes from disposal continuing past failing instances.

## Notes

- **Cold first resolve** now measures the interpreted (reflection) tier: the expression tree is compiled on the
  second activation of a node instead of the first, and singletons are never compiled. The compile cost moved,
  it did not disappear: `Resolve_N_DisposableTransients` (fresh container per iteration, so it includes one
  compile) is unchanged at about 75 to 87 μs.
- **FrozenDictionary was evaluated and not adopted.** For the resolver-cache shape (`Type` keys, ~30 entries),
  `ConcurrentDictionary.TryGetValue` measured 3.2 ns, `FrozenDictionary` 4.5 ns and `Dictionary` 5.3 ns on .NET 11.
