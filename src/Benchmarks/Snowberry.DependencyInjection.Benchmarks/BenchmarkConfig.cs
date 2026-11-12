using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;

namespace Snowberry.DependencyInjection.Benchmarks;

/// <summary>
/// Shared configuration applied to every benchmark in this assembly (passed to <c>BenchmarkSwitcher.Run</c>).
/// The benchmark host is net11.0 only; the library still ships net11/net10/netstandard2.0.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BenchmarkConfig"/> class with the console logger, the default
    /// columns plus rank, the memory diagnoser, and the GitHub Markdown and full JSON exporters.
    /// </summary>
    public BenchmarkConfig()
    {
        // A from-scratch ManualConfig has no defaults, so add the essentials explicitly.
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Memory is an explicit goal of this work, so report Allocated/Gen0 on every benchmark.
        AddDiagnoser(MemoryDiagnoser.Default);

        AddColumn(RankColumn.Arabic);

        // Committable, diffable outputs (raw artifacts stay git-ignored; curated copies live under results\).
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);
    }
}
