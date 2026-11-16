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
        ClearXpingEnvironment();

        var config = DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.Full)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }

    /// <summary>
    /// Removes every <c>XPING_*</c> variable from this process.
    /// </summary>
    /// <remarks>
    /// <c>XPING_*</c> wins over the configuration a benchmark passes to <c>Initialize</c>, so a shell
    /// with <c>XPING_APIKEY</c> set would switch the adapter benchmarks to Cloud mode and upload
    /// synthetic sessions under the real key. BenchmarkDotNet's child processes inherit this
    /// process's environment, so clearing it here covers them too.
    /// </remarks>
    private static void ClearXpingEnvironment()
    {
        foreach (string name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (name.StartsWith("XPING_", StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable(name, null);
        }
    }
}
