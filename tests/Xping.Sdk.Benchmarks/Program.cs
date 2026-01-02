/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Entry point for running Xping SDK performance benchmarks.
/// </summary>
/// <remarks>
/// This program runs BenchmarkDotNet benchmarks to measure the performance
/// characteristics of the Xping SDK, including test execution overhead,
/// memory usage, throughput, and latency.
/// 
/// Usage:
/// - Run all benchmarks: dotnet run -c Release
/// - Run specific benchmark: dotnet run -c Release --filter *CollectorBenchmarks*
/// - Run with profiler: dotnet run -c Release --profiler EP
/// 
/// Results are exported to BenchmarkDotNet.Artifacts/ directory in multiple formats.
/// </remarks>
internal static class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.Full)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
